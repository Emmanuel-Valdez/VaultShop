using Stripe.Checkout;

namespace UkiyoDesignsWeb.Services.Payments
{
	public interface IStripeCheckoutSessionClient
	{
		Session Create(SessionCreateOptions options);
		Session Get(string sessionId);
	}

	public sealed class StripeCheckoutSessionClient : IStripeCheckoutSessionClient
	{
		private readonly SessionService _sessionService = new();

		public Session Create(SessionCreateOptions options)
		{
			return _sessionService.Create(options);
		}

		public Session Get(string sessionId)
		{
			return _sessionService.Get(sessionId);
		}
	}
}
