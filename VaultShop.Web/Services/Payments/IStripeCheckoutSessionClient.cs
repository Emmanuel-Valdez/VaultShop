using Stripe.Checkout;

namespace VaultShop.Web.Services.Payments
{
	public interface IStripeCheckoutSessionClient
	{
		Session Create(SessionCreateOptions options);
		Session Get(string sessionId);
		Session Expire(string sessionId);
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

		public Session Expire(string sessionId)
		{
			return _sessionService.Expire(sessionId);
		}
	}
}
