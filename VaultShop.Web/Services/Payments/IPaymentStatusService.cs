namespace VaultShop.Web.Services.Payments
{
	public interface IPaymentStatusService
	{
		Task<bool> MarkCheckoutSessionPaid(PaymentSessionStatusUpdate update);
		bool MarkCheckoutSessionFailed(PaymentSessionStatusUpdate update);
		Task<bool> ApproveManualBankTransfer(int orderId);
	}

	public sealed record PaymentSessionStatusUpdate(
		int? OrderId,
		string SessionId,
		string? PaymentIntentId);
}
