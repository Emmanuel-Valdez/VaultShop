namespace VaultShop.Web.Services.Email;

public interface ITransactionalEmailService
{
    Task TrySendOrderConfirmationAsync(int orderId);
    Task TrySendPaymentReceiptAsync(int orderId);
    Task TrySendPaymentFailedAsync(int orderId);
    Task TrySendShippingConfirmationAsync(int orderId);
    Task TrySendAdminNewOrderAlertAsync(int orderId);
}