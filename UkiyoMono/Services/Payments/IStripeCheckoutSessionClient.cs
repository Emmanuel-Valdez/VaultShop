using Stripe.Checkout;

namespace UkiyoDesignsWeb.Services.Payments
{
	public interface IStripeCheckoutSessionClient
	{
		Session Create(SessionCreateOptions options);
	}

	public sealed class StripeCheckoutSessionClient : IStripeCheckoutSessionClient
	{
		private readonly SessionService _sessionService = new();

		public Session Create(SessionCreateOptions options)
		{
			return _sessionService.Create(options);
		}
	}
}
