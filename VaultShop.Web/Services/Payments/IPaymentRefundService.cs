using Stripe;

namespace VaultShop.Web.Services.Payments
{
	public interface IPaymentRefundService
	{
		void RefundPaymentIntent(string paymentIntentId);
	}

	public sealed class StripePaymentRefundService : IPaymentRefundService
	{
		private readonly RefundService _refundService = new();

		public void RefundPaymentIntent(string paymentIntentId)
		{
			_refundService.Create(new RefundCreateOptions
			{
				Reason = RefundReasons.RequestedByCustomer,
				PaymentIntent = paymentIntentId
			});
		}
	}
}
