namespace UkiyoDesignsWeb.Services.Payments
{
	public interface IPaymentSessionService
	{
		PaymentSessionResult CreateCheckoutSession(PaymentSessionRequest request);
		PaymentSessionStatusResult GetCheckoutSessionStatus(string sessionId);
		void ExpireCheckoutSession(string sessionId);
	}

	public sealed record PaymentSessionRequest(
		int OrderId,
		IEnumerable<PaymentSessionLineItem> LineItems,
		string SuccessUrl,
		string CancelUrl);

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
		string? PaymentStatus)
	{
		public bool IsPaid => string.Equals(PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase);
	}
}
