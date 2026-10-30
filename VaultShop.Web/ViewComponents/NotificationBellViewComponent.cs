using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VaultShop.DataAccess.Data;
using VaultShop.Utility;

namespace VaultShop.Web.ViewComponents;

public sealed record NotificationBellViewModel(int UnreadCount, string AccessibleLabel);

public sealed class NotificationBellViewComponent : ViewComponent
{
	private readonly ApplicationDbContext _db;

	public NotificationBellViewComponent(ApplicationDbContext db)
	{
		_db = db;
	}

	public async Task<IViewComponentResult> InvokeAsync(string notificationsLabel, string unreadNotificationsLabel)
	{
		var user = HttpContext.User;
		if (user.Identity?.IsAuthenticated != true
			|| !(user.IsInRole(SD.Role_Customer) || user.IsInRole(SD.Role_Company) || user.IsInRole(SD.Role_Admin)))
		{
			return Content(string.Empty);
		}

		var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
		if (string.IsNullOrEmpty(userId))
		{
			return Content(string.Empty);
		}

		var unreadCount = await _db.UserNotifications
			.AsNoTracking()
			.CountAsync(notification => notification.ApplicationUserId == userId && notification.ReadAt == null);
		var label = unreadCount == 0
			? notificationsLabel
			: string.Format(CultureInfo.CurrentCulture, unreadNotificationsLabel, unreadCount);

		return View(new NotificationBellViewModel(unreadCount, label));
	}
}
