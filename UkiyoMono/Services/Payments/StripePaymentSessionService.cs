using Stripe.Checkout;

namespace UkiyoDesignsWeb.Services.Payments
{
	public sealed class StripePaymentSessionService : IPaymentSessionService
	{
		private readonly IStripeCheckoutSessionClient _stripeCheckoutSessionClient;

		public StripePaymentSessionService(IStripeCheckoutSessionClient stripeCheckoutSessionClient)
		{
			_stripeCheckoutSessionClient = stripeCheckoutSessionClient;
		}

		public PaymentSessionResult CreateCheckoutSession(PaymentSessionRequest request)
		{
			var options = new SessionCreateOptions
			{
				SuccessUrl = request.SuccessUrl,
				CancelUrl = request.CancelUrl,
				Mode = "payment",
				LineItems = request.LineItems.Select(item => new SessionLineItemOptions
				{
					PriceData = new SessionLineItemPriceDataOptions
					{
						UnitAmount = (long)(item.UnitPrice * 100),
						Currency = "usd",
						ProductData = new SessionLineItemPriceDataProductDataOptions
						{
							Name = item.ProductName
						}
					},
					Quantity = item.Quantity
				}).ToList(),
				Metadata = new Dictionary<string, string>
				{
					["orderId"] = request.OrderId.ToString()
				}
			};

			var session = _stripeCheckoutSessionClient.Create(options);
			return new PaymentSessionResult(session.Id, session.PaymentIntentId, session.Url);
		}
	}
}
