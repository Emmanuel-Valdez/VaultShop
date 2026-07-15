namespace VaultShop.Web.Services.Payments
{
	public interface IPaymentSessionService
	{
		PaymentSessionResult CreateCheckoutSession(PaymentSessionRequest request);
		PaymentSessionStatusResult GetCheckoutSessionStatus(string sessionId, string? providerPaymentId = null);
		void ExpireCheckoutSession(string sessionId);
	}

	public sealed record PaymentSessionRequest(
		int OrderId,
		IEnumerable<PaymentSessionLineItem> LineItems,
		string SuccessUrl,
		string CancelUrl,
		string? NotificationUrl = null);

	public sealed record PaymentSessionLineItem(
		string ProductName,
		decimal UnitPrice,
		int Quantity);

	public sealed record PaymentSessionResult(
		string SessionId,
		string? PaymentIntentId,
		string Url);

	public sealed record PaymentSessionStatusResult(
		string SessionId,
		string? PaymentIntentId,
		string? PaymentStatus,
		string? ExternalReference = null,
		decimal? TransactionAmount = null)
	{
		public bool IsPaid => string.Equals(PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase);
	}
}
