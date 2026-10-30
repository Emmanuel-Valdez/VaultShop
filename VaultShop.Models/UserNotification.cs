namespace VaultShop.Models;

public enum OrderNotificationType
{
	PaymentApproved = 0,
	PaymentFailed = 1,
	Shipped = 2,
	Cancelled = 3,
	Refunded = 4,
	AdminNewOrder = 5,
	AdminBankTransferRequest = 6
}

public class UserNotification
{
	public int Id { get; set; }
	public string ApplicationUserId { get; set; } = string.Empty;
	public int OrderId { get; set; }
	public OrderNotificationType Type { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime? ReadAt { get; set; }
	public OrderHeader OrderHeader { get; set; } = null!;
}
