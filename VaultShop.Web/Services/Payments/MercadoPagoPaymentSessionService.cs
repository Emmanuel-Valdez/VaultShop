using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VaultShop.Web.Services.Payments
{
	public sealed class MercadoPagoPaymentSessionService : IPaymentSessionService
	{
		internal const string HttpClientName = "MercadoPago";

		private readonly IHttpClientFactory _httpClientFactory;
		private readonly ILogger<MercadoPagoPaymentSessionService> _logger;

		public MercadoPagoPaymentSessionService(
			IHttpClientFactory httpClientFactory,
			ILogger<MercadoPagoPaymentSessionService> logger)
		{
			_httpClientFactory = httpClientFactory;
			_logger = logger;
		}

		public PaymentSessionResult CreateCheckoutSession(PaymentSessionRequest request)
		{
			using var client = CreateConfiguredClient();
			// ponytail: raw HttpClient is enough here because Mercado Pago v1 only needs create-preference and payment-search; add a dedicated abstraction only if retries/auth rules or more endpoints appear.
			var payload = new
			{
				external_reference = request.OrderId.ToString(CultureInfo.InvariantCulture),
				back_urls = new
				{
					success = request.SuccessUrl,
					failure = request.CancelUrl,
					pending = request.CancelUrl
				},
				auto_return = "approved",
				notification_url = request.NotificationUrl,
				items = request.LineItems.Select(item => new
				{
					title = item.ProductName,
					quantity = item.Quantity,
					unit_price = item.UnitPrice
				}).ToArray()
			};

			using var response = client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/checkout/preferences")
			{
				Content = new StringContent(JsonSerializer.Serialize(payload, new JsonSerializerOptions
				{
					DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
				}), Encoding.UTF8, "application/json")
			}).GetAwaiter().GetResult();

			var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			EnsureSuccess(response, body, "create checkout preference");

			using var json = JsonDocument.Parse(body);
			var root = json.RootElement;
			var preferenceId = GetRequiredString(root, "id");
			var initPoint = GetRequiredString(root, "init_point");

			return new PaymentSessionResult(preferenceId, null, initPoint);
		}

		public PaymentSessionStatusResult GetCheckoutSessionStatus(string sessionId, string? providerPaymentId = null)
		{
			using var client = CreateConfiguredClient();
			var requestUrl = string.IsNullOrWhiteSpace(providerPaymentId)
				? $"/v1/payments/search?sort=date_created&criteria=desc&limit=1&preference_id={Uri.EscapeDataString(sessionId)}"
				: $"/v1/payments/{Uri.EscapeDataString(providerPaymentId)}";
			using var response = client.SendAsync(new HttpRequestMessage(HttpMethod.Get, requestUrl)).GetAwaiter().GetResult();

			var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			EnsureSuccess(response, body, string.IsNullOrWhiteSpace(providerPaymentId) ? "search payments" : "get payment");

			using var json = JsonDocument.Parse(body);
			if (!string.IsNullOrWhiteSpace(providerPaymentId))
			{
				return ParsePayment(sessionId, json.RootElement);
			}

			if (!json.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
			{
				return new PaymentSessionStatusResult(sessionId, null, null);
			}

			return ParsePayment(sessionId, results[0]);
		}

		private static PaymentSessionStatusResult ParsePayment(string sessionId, JsonElement payment)
		{
			var mercadoPagoStatus = GetOptionalString(payment, "status");
			var paymentStatus = string.Equals(mercadoPagoStatus, "approved", StringComparison.OrdinalIgnoreCase)
				? "paid"
				: mercadoPagoStatus;

			return new PaymentSessionStatusResult(
				sessionId,
				GetOptionalString(payment, "id"),
				paymentStatus,
				GetOptionalString(payment, "external_reference"),
				GetOptionalDecimal(payment, "transaction_amount"));
		}

		public void ExpireCheckoutSession(string sessionId)
		{
			_logger.LogInformation("Mercado Pago checkout preference {SessionId} is not explicitly expired because this integration currently relies on provider-side lifecycle only.", sessionId);
		}

		private HttpClient CreateConfiguredClient()
		{
			var client = _httpClientFactory.CreateClient(HttpClientName);
			if (client.DefaultRequestHeaders.Authorization is null || string.IsNullOrWhiteSpace(client.DefaultRequestHeaders.Authorization.Parameter))
			{
				throw new InvalidOperationException("Missing required Payments:MercadoPagoAccessToken configuration.");
			}

			return client;
		}

		private static void EnsureSuccess(HttpResponseMessage response, string body, string operation)
		{
			if (response.IsSuccessStatusCode)
			{
				return;
			}

			throw new HttpRequestException($"Mercado Pago {operation} failed with status code {(int)response.StatusCode}: {body}", null, response.StatusCode);
		}

		private static string GetRequiredString(JsonElement element, string propertyName)
		{
			var value = GetOptionalString(element, propertyName);
			return string.IsNullOrWhiteSpace(value)
				? throw new InvalidOperationException($"Mercado Pago response did not include required '{propertyName}'.")
				: value;
		}

		private static string? GetOptionalString(JsonElement element, string propertyName)
		{
			if (!element.TryGetProperty(propertyName, out var property))
			{
				return null;
			}

			return property.ValueKind switch
			{
				JsonValueKind.String => property.GetString(),
				JsonValueKind.Number => property.GetRawText(),
				JsonValueKind.True => bool.TrueString,
				JsonValueKind.False => bool.FalseString,
				_ => property.GetRawText()
			};
		}

		private static decimal? GetOptionalDecimal(JsonElement element, string propertyName)
		{
			if (!element.TryGetProperty(propertyName, out var property))
			{
				return null;
			}

			return property.ValueKind switch
			{
				JsonValueKind.Number when property.TryGetDecimal(out var value) => value,
				JsonValueKind.String when decimal.TryParse(property.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) => value,
				_ => null
			};
		}
	}
}
