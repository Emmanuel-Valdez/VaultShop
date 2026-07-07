namespace VaultShop.Web.Services.Payments
{
	public interface IPaymentStatusService
	{
		bool MarkCheckoutSessionPaid(PaymentSessionStatusUpdate update);
		bool MarkCheckoutSessionFailed(PaymentSessionStatusUpdate update);
		bool ApproveManualBankTransfer(int orderId);
	}

	public sealed record PaymentSessionStatusUpdate(
		int? OrderId,
		string SessionId,
		string? PaymentIntentId);
}
