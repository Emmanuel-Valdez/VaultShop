using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Utility;

namespace VaultShop.Web.Services.Notifications;

public sealed class OrderNotificationService : IOrderNotificationService
{
	private readonly IUnitOfWork _unitOfWork;
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly ILogger<OrderNotificationService> _logger;

	public OrderNotificationService(
		IUnitOfWork unitOfWork,
		UserManager<ApplicationUser> userManager,
		ILogger<OrderNotificationService> logger)
	{
		_unitOfWork = unitOfWork;
		_userManager = userManager;
		_logger = logger;
	}

	public async Task NotifyOrderEventAsync(int orderId, OrderNotificationType type)
	{
		if (!IsCustomerEvent(type) && !IsAdminEvent(type))
		{
			_logger.LogWarning("Ignored unsupported order notification type {NotificationType} for order {OrderId}.", type, orderId);
			return;
		}

		try
		{
			var order = _unitOfWork.OrderHeader.Get(order => order.Id == orderId);
			if (order is null)
			{
				_logger.LogWarning("Could not find order {OrderId} for notification type {NotificationType}.", orderId, type);
				return;
			}

			IEnumerable<string> recipientIds;
			if (IsAdminEvent(type))
			{
				recipientIds = (await _userManager.GetUsersInRoleAsync(SD.Role_Admin)).Select(user => user.Id);
			}
			else if (!string.IsNullOrWhiteSpace(order.ApplicationUserId))
			{
				recipientIds = [order.ApplicationUserId];
			}
			else
			{
				return;
			}

			foreach (var userId in recipientIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal))
			{
				CreateIfMissing(userId, orderId, type);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to process order notification type {NotificationType} for order {OrderId}.", type, orderId);
		}
	}

	private void CreateIfMissing(string userId, int orderId, OrderNotificationType type)
	{
		var repository = _unitOfWork.UserNotification;
		UserNotification? notification = null;
		var added = false;
		try
		{
			if (repository.Get(item => item.ApplicationUserId == userId && item.OrderId == orderId && item.Type == type) is not null)
			{
				return;
			}

			notification = new UserNotification
			{
				ApplicationUserId = userId,
				OrderId = orderId,
				Type = type
			};
			repository.Add(notification);
			added = true;
			_unitOfWork.Save();
		}
		catch (DbUpdateException ex) when (added)
		{
			DetachAfterFailedSave(repository, notification!);
			if (DuplicateExists(repository, userId, orderId, type))
			{
				return;
			}

			_logger.LogError(ex, "Failed to persist order notification type {NotificationType} for order {OrderId} and user {UserId}.", type, orderId, userId);
		}
		catch (Exception ex)
		{
			if (added)
			{
				DetachAfterFailedSave(repository, notification!);
			}

			_logger.LogError(ex, "Failed to persist order notification type {NotificationType} for order {OrderId} and user {UserId}.", type, orderId, userId);
		}
	}

	private bool DuplicateExists(IUserNotificationRepository repository, string userId, int orderId, OrderNotificationType type)
	{
		try
		{
			return repository.Get(item => item.ApplicationUserId == userId && item.OrderId == orderId && item.Type == type) is not null;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not check for an existing order notification type {NotificationType} for order {OrderId} and user {UserId}.", type, orderId, userId);
			return false;
		}
	}

	private void DetachAfterFailedSave(IUserNotificationRepository repository, UserNotification notification)
	{
		try
		{
			repository.Remove(notification);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not detach a failed order notification for order {OrderId} and user {UserId}.", notification.OrderId, notification.ApplicationUserId);
		}
	}

	private static bool IsCustomerEvent(OrderNotificationType type) => type is
		OrderNotificationType.PaymentApproved or
		OrderNotificationType.PaymentFailed or
		OrderNotificationType.Shipped or
		OrderNotificationType.Cancelled or
		OrderNotificationType.Refunded;

	private static bool IsAdminEvent(OrderNotificationType type) => type is
		OrderNotificationType.AdminNewOrder or
		OrderNotificationType.AdminBankTransferRequest;
}
