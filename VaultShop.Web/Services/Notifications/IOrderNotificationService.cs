using VaultShop.Models;

namespace VaultShop.Web.Services.Notifications;

public interface IOrderNotificationService
{
	Task NotifyOrderEventAsync(int orderId, OrderNotificationType type);
}
