using UkiyoDesigns.Models;

namespace UkiyoDesignsWeb.Services.Payments
{
	public interface IPaymentSessionService
	{
		PaymentSessionResult CreateCheckoutSession(PaymentSessionRequest request);
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
}
