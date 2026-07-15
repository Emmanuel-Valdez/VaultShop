using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using VaultShop.Web.Services.Payments;

namespace VaultShop.Web.Tests
{
	public class MercadoPagoPaymentSessionServiceTests
	{
		[Fact]
		public void CreateCheckoutSession_ReturnsPreferenceIdAndInitPoint()
		{
			var handler = new StubHttpMessageHandler((request, _) =>
			{
				Assert.Equal(HttpMethod.Post, request.Method);
				Assert.Equal("https://api.mercadopago.com/checkout/preferences", request.RequestUri?.ToString());
				Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
				Assert.Equal("test-token", request.Headers.Authorization?.Parameter);

				var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
				Assert.Contains("\"external_reference\":\"42\"", body);
				Assert.Contains("\"success\":\"https://example.test/success\"", body);
				Assert.Contains("\"failure\":\"https://example.test/cancel\"", body);
				Assert.Contains("\"auto_return\":\"approved\"", body);
				Assert.Contains("\"notification_url\":\"https://example.test/api/mercadopago/webhook?source_news=webhooks\"", body);
				Assert.Contains("\"title\":\"Kimono\"", body);
				Assert.Contains("\"unit_price\":100.5", body);
				Assert.Contains("\"quantity\":2", body);

				return new HttpResponseMessage(HttpStatusCode.Created)
				{
					Content = new StringContent("{\"id\":\"pref_123\",\"init_point\":\"https://mp.test/checkout\"}", Encoding.UTF8, "application/json")
				};
			});

			var service = CreateService(handler);
			var request = new PaymentSessionRequest(
				42,
				[new PaymentSessionLineItem("Kimono", 100.5m, 2)],
				"https://example.test/success",
				"https://example.test/cancel",
				"https://example.test/api/mercadopago/webhook?source_news=webhooks");

			var result = service.CreateCheckoutSession(request);

			Assert.Equal("pref_123", result.SessionId);
			Assert.Null(result.PaymentIntentId);
			Assert.Equal("https://mp.test/checkout", result.Url);
		}

		[Fact]
		public void GetCheckoutSessionStatus_ApprovedPayment_IsPaid()
		{
			var handler = new StubHttpMessageHandler((request, _) =>
			{
				Assert.Equal(HttpMethod.Get, request.Method);
				Assert.Equal("https://api.mercadopago.com/v1/payments/search?sort=date_created&criteria=desc&limit=1&preference_id=pref_approved", request.RequestUri?.ToString());

				return new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent("{\"results\":[{\"id\":987654321,\"status\":\"approved\"}]}", Encoding.UTF8, "application/json")
				};
			});

			var service = CreateService(handler);

			var result = service.GetCheckoutSessionStatus("pref_approved");

			Assert.Equal("pref_approved", result.SessionId);
			Assert.Equal("987654321", result.PaymentIntentId);
			Assert.Equal("paid", result.PaymentStatus);
			Assert.True(result.IsPaid);
		}

		[Fact]
		public void GetCheckoutSessionStatus_WithPaymentId_UsesDirectPaymentAndReturnsVerifiedFields()
		{
			var handler = new StubHttpMessageHandler((request, _) =>
			{
				Assert.Equal(HttpMethod.Get, request.Method);
				Assert.Equal("https://api.mercadopago.com/v1/payments/987654321", request.RequestUri?.ToString());

				return new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent("{\"id\":987654321,\"status\":\"approved\",\"external_reference\":\"42\",\"transaction_amount\":201.00}", Encoding.UTF8, "application/json")
				};
			});
			var service = CreateService(handler);

			var result = service.GetCheckoutSessionStatus("pref_approved", "987654321");

			Assert.Equal("pref_approved", result.SessionId);
			Assert.Equal("987654321", result.PaymentIntentId);
			Assert.Equal("paid", result.PaymentStatus);
			Assert.Equal("42", result.ExternalReference);
			Assert.Equal(201m, result.TransactionAmount);
			Assert.True(result.IsPaid);
		}

		[Fact]
		public void GetCheckoutSessionStatus_NoPayments_IsNotPaid()
		{
			var handler = new StubHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("{\"results\":[]}", Encoding.UTF8, "application/json")
			});

			var service = CreateService(handler);

			var result = service.GetCheckoutSessionStatus("pref_missing");

			Assert.Equal("pref_missing", result.SessionId);
			Assert.Null(result.PaymentIntentId);
			Assert.Null(result.PaymentStatus);
			Assert.False(result.IsPaid);
		}

		private static MercadoPagoPaymentSessionService CreateService(HttpMessageHandler handler)
		{
			var httpClient = new HttpClient(handler)
			{
				BaseAddress = new Uri("https://api.mercadopago.com")
			};
			httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
			var factory = new StubHttpClientFactory(httpClient);
			return new MercadoPagoPaymentSessionService(factory, Mock.Of<ILogger<MercadoPagoPaymentSessionService>>());
		}

		private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
		{
			public HttpClient CreateClient(string name)
			{
				Assert.Equal("MercadoPago", name);
				return client;
			}
		}

		private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler) : HttpMessageHandler
		{
			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				return Task.FromResult(handler(request, cancellationToken));
			}
		}
	}
}
