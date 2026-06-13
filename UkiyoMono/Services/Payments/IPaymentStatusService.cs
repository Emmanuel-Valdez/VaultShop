namespace UkiyoDesignsWeb.Services.Payments
{
	public interface IPaymentStatusService
	{
		bool MarkCheckoutSessionPaid(PaymentSessionStatusUpdate update);
		bool MarkCheckoutSessionFailed(PaymentSessionStatusUpdate update);
	}

	public sealed record PaymentSessionStatusUpdate(
		int? OrderId,
		string SessionId,
		string? PaymentIntentId);
}
