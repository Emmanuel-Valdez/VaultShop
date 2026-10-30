using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VaultShop.DataAccess.Data;
using VaultShop.Models;

namespace VaultShop.Web.Areas.Identity.Pages.Account;

[Authorize]
public class NotificationsModel : PageModel
{
	private const int MaxNotifications = 100;

	private readonly ApplicationDbContext _db;

	public NotificationsModel(ApplicationDbContext db)
	{
		_db = db;
	}

	public IReadOnlyList<UserNotification> Notifications { get; private set; } = [];

	public static (string TitleKey, string DetailKey) GetLocalizationKeys(OrderNotificationType type)
		=> type switch
		{
			OrderNotificationType.PaymentApproved => ("PaymentApprovedTitle", "PaymentApprovedDetail"),
			OrderNotificationType.PaymentFailed => ("PaymentFailedTitle", "PaymentFailedDetail"),
			OrderNotificationType.Shipped => ("ShippedTitle", "ShippedDetail"),
			OrderNotificationType.Cancelled => ("CancelledTitle", "CancelledDetail"),
			OrderNotificationType.Refunded => ("RefundedTitle", "RefundedDetail"),
			OrderNotificationType.AdminNewOrder => ("AdminNewOrderTitle", "AdminNewOrderDetail"),
			OrderNotificationType.AdminBankTransferRequest => ("AdminBankTransferRequestTitle", "AdminBankTransferRequestDetail"),
			_ => ("OrderUpdateTitle", "OrderUpdateDetail"),
		};

	public async Task OnGetAsync()
	{
		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
		if (string.IsNullOrEmpty(userId))
		{
			return;
		}

		Notifications = await _db.UserNotifications
			.AsNoTracking()
			.Include(notification => notification.OrderHeader)
			.Where(notification => notification.ApplicationUserId == userId)
			.OrderByDescending(notification => notification.CreatedAt)
			.ThenByDescending(notification => notification.Id)
			.Take(MaxNotifications)
			.ToListAsync();

		if (Notifications.Any(notification => notification.ReadAt == null))
		{
			await _db.UserNotifications
				.Where(notification => notification.ApplicationUserId == userId && notification.ReadAt == null)
				.ExecuteUpdateAsync(setters => setters.SetProperty(
					notification => notification.ReadAt, (DateTime?)DateTime.UtcNow));
		}
	}
}
