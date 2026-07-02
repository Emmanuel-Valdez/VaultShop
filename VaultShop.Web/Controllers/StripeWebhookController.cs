using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using UkiyoDesigns.Utility;
using UkiyoDesignsWeb.Services.Payments;

namespace UkiyoDesignsWeb.Controllers
{
	[ApiController]
	[Route("api/stripe/webhook")]
	[IgnoreAntiforgeryToken]
	public sealed class StripeWebhookController : ControllerBase
	{
		private readonly IPaymentStatusService _paymentStatusService;
		private readonly StripeSettings _stripeSettings;
		private readonly ILogger<StripeWebhookController> _logger;

		public StripeWebhookController(
			IPaymentStatusService paymentStatusService,
			IOptions<StripeSettings> stripeSettings,
			ILogger<StripeWebhookController> logger)
		{
			_paymentStatusService = paymentStatusService;
			_stripeSettings = stripeSettings.Value;
			_logger = logger;
		}

		[HttpPost]
		public async Task<IActionResult> Post()
		{
			if (string.IsNullOrWhiteSpace(_stripeSettings.WebhookSecret))
			{
				_logger.LogError("Stripe webhook secret is not configured.");
				return StatusCode(StatusCodes.Status500InternalServerError);
			}

			var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
			Event stripeEvent;

			try
			{
				stripeEvent = EventUtility.ConstructEvent(
					json,
					Request.Headers["Stripe-Signature"],
					_stripeSettings.WebhookSecret);
			}
			catch (StripeException ex)
			{
				_logger.LogWarning(ex, "Rejected Stripe webhook with invalid signature or payload.");
				return BadRequest();
			}

			if (stripeEvent.Data.Object is not Session session)
			{
				_logger.LogInformation("Ignored Stripe webhook event {EventType} because it did not contain a checkout session.", stripeEvent.Type);
				return Ok();
			}

			var update = new PaymentSessionStatusUpdate(TryGetOrderId(session), session.Id, session.PaymentIntentId);

			switch (stripeEvent.Type)
			{
				case Events.CheckoutSessionCompleted:
					if (string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
					{
						_paymentStatusService.MarkCheckoutSessionPaid(update);
					}
					break;
				case Events.CheckoutSessionAsyncPaymentFailed:
					_paymentStatusService.MarkCheckoutSessionFailed(update);
					break;
				default:
					_logger.LogInformation("Ignored Stripe webhook event {EventType}.", stripeEvent.Type);
					break;
			}

			return Ok();
		}

		private static int? TryGetOrderId(Session session)
		{
			if (session.Metadata != null &&
				session.Metadata.TryGetValue("orderId", out var orderIdValue) &&
				int.TryParse(orderIdValue, out var orderId))
			{
				return orderId;
			}

			return null;
		}
	}
}
