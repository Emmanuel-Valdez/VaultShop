using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Utility;
using VaultShop.Web.Services.Email;
using VaultShop.Web.Services.Payments;

namespace VaultShop.Web.Controllers
{
	[ApiController]
	[Route("api/mercadopago/webhook")]
	[IgnoreAntiforgeryToken]
	[AllowAnonymous]
	public sealed class MercadoPagoWebhookController : ControllerBase
	{
		private readonly IConfiguration _configuration;
		private readonly IServiceProvider _paymentSessionServiceProvider;
		private readonly IUnitOfWork _unitOfWork;
		private readonly IPaymentStatusService _paymentStatusService;
		private readonly ITransactionalEmailService _emailService;
		private readonly ILogger<MercadoPagoWebhookController> _logger;

		public MercadoPagoWebhookController(
			IConfiguration configuration,
			IServiceProvider paymentSessionServiceProvider,
			IUnitOfWork unitOfWork,
			IPaymentStatusService paymentStatusService,
			ITransactionalEmailService emailService,
			ILogger<MercadoPagoWebhookController> logger)
		{
			_configuration = configuration;
			_paymentSessionServiceProvider = paymentSessionServiceProvider;
			_unitOfWork = unitOfWork;
			_paymentStatusService = paymentStatusService;
			_emailService = emailService;
			_logger = logger;
		}

		[HttpPost]
		public async Task<IActionResult> Post()
		{
			var secret = _configuration["Payments:MercadoPagoWebhookSecret"];
			if (string.IsNullOrWhiteSpace(secret))
			{
				_logger.LogError("Mercado Pago webhook secret is not configured.");
				return StatusCode(StatusCodes.Status500InternalServerError);
			}

			var paymentId = Request.Query["data.id"].ToString();
			if (!MercadoPagoSignatureValidator.IsValid(
				paymentId,
				Request.Headers["x-request-id"].ToString(),
				Request.Headers["x-signature"].ToString(),
				secret))
			{
				_logger.LogWarning("Rejected Mercado Pago webhook with an invalid signature.");
				return BadRequest();
			}

			PaymentSessionStatusResult payment;
			try
			{
				var paymentService = _paymentSessionServiceProvider.GetRequiredKeyedService<IPaymentSessionService>(SD.PaymentMethodMercadoPago);
				payment = paymentService.GetCheckoutSessionStatus(string.Empty, paymentId);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Could not fetch Mercado Pago payment {PaymentId} for webhook processing.", paymentId);
				return StatusCode(StatusCodes.Status500InternalServerError);
			}

			if (!payment.IsPaid)
			{
				return Ok();
			}

			if (!int.TryParse(payment.ExternalReference, NumberStyles.None, CultureInfo.InvariantCulture, out var orderId))
			{
				_logger.LogWarning("Ignored Mercado Pago payment {PaymentId} with invalid external reference {ExternalReference}.", paymentId, payment.ExternalReference);
				return Ok();
			}

			var order = _unitOfWork.OrderHeader.Get(item => item.Id == orderId);
			if (order == null || order.PaymentMethod != SD.PaymentMethodMercadoPago ||
				payment.TransactionAmount != order.OrderTotal || string.IsNullOrWhiteSpace(order.SessionId))
			{
				_logger.LogWarning("Ignored Mercado Pago payment {PaymentId} for order {OrderId} because the order, provider, amount, or stored preference did not match.", paymentId, orderId);
				return Ok();
			}
			if (order.PaymentStatus == SD.PaymentStatusApproved &&
				!string.IsNullOrWhiteSpace(order.PaymentIntentId) &&
				!string.IsNullOrWhiteSpace(payment.PaymentIntentId) &&
				!string.Equals(order.PaymentIntentId, payment.PaymentIntentId, StringComparison.Ordinal))
			{
				_logger.LogError("Approved Mercado Pago payment {PaymentId} arrived for already-approved order {OrderId} with stored payment {StoredPaymentId}. Possible duplicate charge; manual refund review required.", payment.PaymentIntentId, order.Id, order.PaymentIntentId);
			}

			var markedPaid = _paymentStatusService.MarkCheckoutSessionPaid(
				new PaymentSessionStatusUpdate(order.Id, order.SessionId, payment.PaymentIntentId));
			if (markedPaid)
			{
				await _emailService.TrySendPaymentReceiptAsync(order.Id);
			}

			return Ok();
		}
	}

	internal static class MercadoPagoSignatureValidator
	{
		public static bool IsValid(string paymentId, string requestId, string signature, string secret)
		{
			if (string.IsNullOrWhiteSpace(paymentId) || string.IsNullOrWhiteSpace(requestId) ||
				string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(secret))
			{
				return false;
			}

			string? timestamp = null;
			string? suppliedHash = null;
			foreach (var part in signature.Split(','))
			{
				var pair = part.Trim().Split('=', 2);
				if (pair.Length != 2)
				{
					continue;
				}

				if (string.Equals(pair[0], "ts", StringComparison.OrdinalIgnoreCase)) timestamp = pair[1];
				if (string.Equals(pair[0], "v1", StringComparison.OrdinalIgnoreCase)) suppliedHash = pair[1];
			}

			if (string.IsNullOrWhiteSpace(timestamp) || string.IsNullOrWhiteSpace(suppliedHash))
			{
				return false;
			}

			var manifest = $"id:{paymentId.ToLowerInvariant()};request-id:{requestId};ts:{timestamp};";
			var expectedHash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest));
			try
			{
				return CryptographicOperations.FixedTimeEquals(expectedHash, Convert.FromHexString(suppliedHash));
			}
			catch (FormatException)
			{
				return false;
			}
		}
	}
}
