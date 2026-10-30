using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Utility;
using VaultShop.Web.Services.Email;
using VaultShop.Web.Services.Notifications;

namespace VaultShop.Web.Services.Payments
{
	public sealed class PaymentStatusService : IPaymentStatusService
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly ITransactionalEmailService _emailService;
		private readonly IOrderNotificationService _orderNotificationService;
		private readonly ILogger<PaymentStatusService> _logger;

		public PaymentStatusService(
			IUnitOfWork unitOfWork,
			ITransactionalEmailService emailService,
			IOrderNotificationService orderNotificationService,
			ILogger<PaymentStatusService> logger)
		{
			_unitOfWork = unitOfWork;
			_emailService = emailService;
			_orderNotificationService = orderNotificationService;
			_logger = logger;
		}

		public async Task<bool> MarkCheckoutSessionPaid(PaymentSessionStatusUpdate update)
		{
			var orderHeader = FindOrder(update);
			if (orderHeader == null)
			{
				_logger.LogWarning("Could not find order for paid checkout session {SessionId}.", update.SessionId);
				return false;
			}
			if (!SessionMatches(orderHeader, update))
			{
				_logger.LogWarning("Ignored paid checkout session {SessionId} for order {OrderId} because the order currently stores session {StoredSessionId}.", update.SessionId, orderHeader.Id, orderHeader.SessionId);
				return false;
			}
			if (orderHeader.PaymentStatus == SD.PaymentStatusApproved)
			{
				_logger.LogInformation("Ignored duplicate paid checkout session {SessionId} for already-paid order {OrderId}.", update.SessionId, orderHeader.Id);
				return true;
			}
			if (IsTerminal(orderHeader))
			{
				_logger.LogError("Paid checkout session {SessionId} arrived for terminal order {OrderId}. OrderStatus: {OrderStatus}. PaymentStatus: {PaymentStatus}. Manual review/refund may be required.", update.SessionId, orderHeader.Id, orderHeader.OrderStatus, orderHeader.PaymentStatus);
				return false;
			}
			if (!IsPayable(orderHeader))
			{
				_logger.LogWarning("Ignored paid checkout session {SessionId} for order {OrderId} in non-payable state. OrderStatus: {OrderStatus}. PaymentStatus: {PaymentStatus}.", update.SessionId, orderHeader.Id, orderHeader.OrderStatus, orderHeader.PaymentStatus);
				return false;
			}

			_unitOfWork.OrderHeader.UpdateStripePaymentId(orderHeader.Id, update.SessionId, update.PaymentIntentId ?? string.Empty);
			var nextOrderStatus = orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment
				? (orderHeader.OrderStatus is SD.StatusInProcess or SD.StatusShipped ? orderHeader.OrderStatus! : SD.StatusApproved)
				: SD.StatusApproved;
			_unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, nextOrderStatus, SD.PaymentStatusApproved);
			_unitOfWork.Save();
			_logger.LogInformation("Marked order {OrderId} as paid from checkout session {SessionId}.", orderHeader.Id, update.SessionId);
			await _orderNotificationService.NotifyOrderEventAsync(orderHeader.Id, OrderNotificationType.PaymentApproved);
			if (orderHeader.CompanyId.GetValueOrDefault() == 0)
			{
				await _orderNotificationService.NotifyOrderEventAsync(orderHeader.Id, OrderNotificationType.AdminNewOrder);
			}
			// ponytail: emails after real transition only; duplicate early return above must not send
			try { await _emailService.TrySendOrderConfirmationAsync(orderHeader.Id); }
			catch (Exception ex) { _logger.LogError(ex, "Failed to send order confirmation for paid order {OrderId}.", orderHeader.Id); }
			// ponytail: admin already notified at creation for wholesale — suppress duplicate
			if (orderHeader.CompanyId.GetValueOrDefault() == 0)
			{
				try { await _emailService.TrySendAdminNewOrderAlertAsync(orderHeader.Id); }
				catch (Exception ex) { _logger.LogError(ex, "Failed to send admin alert for paid order {OrderId}.", orderHeader.Id); }
			}
			return true;
		}

		public async Task<bool> MarkCheckoutSessionFailed(PaymentSessionStatusUpdate update)
		{
			var orderHeader = FindOrder(update);
			if (orderHeader == null)
			{
				_logger.LogWarning("Could not find order for failed checkout session {SessionId}.", update.SessionId);
				return false;
			}
			if (!SessionMatches(orderHeader, update))
			{
				_logger.LogWarning("Ignored failed checkout session {SessionId} for order {OrderId} because the order currently stores session {StoredSessionId}.", update.SessionId, orderHeader.Id, orderHeader.SessionId);
				return false;
			}
			if (orderHeader.PaymentStatus == SD.PaymentStatusApproved)
			{
				_logger.LogError("Failed checkout session {SessionId} arrived for already-paid order {OrderId}. Ignored to avoid cancelling a paid order; manual review may be required.", update.SessionId, orderHeader.Id);
				return false;
			}
			if (IsTerminal(orderHeader))
			{
				_logger.LogInformation("Ignored failed checkout session {SessionId} for terminal order {OrderId}. OrderStatus: {OrderStatus}. PaymentStatus: {PaymentStatus}.", update.SessionId, orderHeader.Id, orderHeader.OrderStatus, orderHeader.PaymentStatus);
				return false;
			}
			if (!IsPayable(orderHeader))
			{
				_logger.LogWarning("Ignored failed checkout session {SessionId} for order {OrderId} in non-payable state. OrderStatus: {OrderStatus}. PaymentStatus: {PaymentStatus}.", update.SessionId, orderHeader.Id, orderHeader.OrderStatus, orderHeader.PaymentStatus);
				return false;
			}

			_unitOfWork.OrderHeader.UpdateStripePaymentId(orderHeader.Id, update.SessionId, update.PaymentIntentId ?? string.Empty);
			_unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, SD.StatusCancelled, SD.PaymentStatusRejected);
			_unitOfWork.Save();
			_logger.LogInformation("Marked order {OrderId} as payment failed from checkout session {SessionId}.", orderHeader.Id, update.SessionId);
			await _orderNotificationService.NotifyOrderEventAsync(orderHeader.Id, OrderNotificationType.PaymentFailed);
			return true;
		}

		public async Task<bool> ApproveManualBankTransfer(int orderId)
		{
			var orderHeader = _unitOfWork.OrderHeader.Get(o => o.Id == orderId, tracked: true);
			if (orderHeader == null)
			{
				_logger.LogWarning("Could not find order {OrderId} for manual bank transfer approval.", orderId);
				return false;
			}
			if (orderHeader.PaymentMethod != SD.PaymentMethodBankTransfer)
			{
				_logger.LogWarning("Rejected manual bank transfer approval for order {OrderId} because its payment method is {PaymentMethod}.", orderId, orderHeader.PaymentMethod);
				return false;
			}
			if (orderHeader.PaymentStatus == SD.PaymentStatusApproved)
			{
				_logger.LogInformation("Ignored duplicate bank transfer approval for already-paid order {OrderId}.", orderId);
				return true;
			}
			if (IsTerminal(orderHeader))
			{
				_logger.LogError("Rejected manual bank transfer approval for terminal order {OrderId}. OrderStatus: {OrderStatus}. PaymentStatus: {PaymentStatus}.", orderId, orderHeader.OrderStatus, orderHeader.PaymentStatus);
				return false;
			}
			if (!IsPayable(orderHeader))
			{
				_logger.LogWarning("Rejected manual bank transfer approval for order {OrderId} in non-payable state. OrderStatus: {OrderStatus}. PaymentStatus: {PaymentStatus}.", orderId, orderHeader.OrderStatus, orderHeader.PaymentStatus);
				return false;
			}

			var nextOrderStatus = orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment
				? (orderHeader.OrderStatus is SD.StatusInProcess or SD.StatusShipped ? orderHeader.OrderStatus! : SD.StatusApproved)
				: SD.StatusApproved;
			orderHeader.PaymentDate = DateTime.UtcNow;
			_unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, nextOrderStatus, SD.PaymentStatusApproved);
			_unitOfWork.Save();
			_logger.LogInformation("Approved manual bank transfer for order {OrderId}.", orderId);
			await _orderNotificationService.NotifyOrderEventAsync(orderId, OrderNotificationType.PaymentApproved);
			if (orderHeader.CompanyId.GetValueOrDefault() == 0)
			{
				await _orderNotificationService.NotifyOrderEventAsync(orderId, OrderNotificationType.AdminNewOrder);
			}
			// ponytail: emails after real transition only; duplicate early return above must not send
			try { await _emailService.TrySendOrderConfirmationAsync(orderId); }
			catch (Exception ex) { _logger.LogError(ex, "Failed to send order confirmation for approved bank-transfer order {OrderId}.", orderId); }
			// ponytail: admin already notified at creation for wholesale — suppress duplicate
			if (orderHeader.CompanyId.GetValueOrDefault() == 0)
			{
				try { await _emailService.TrySendAdminNewOrderAlertAsync(orderId); }
				catch (Exception ex) { _logger.LogError(ex, "Failed to send admin alert for approved bank-transfer order {OrderId}.", orderId); }
			}
			return true;
		}

		private OrderHeader? FindOrder(PaymentSessionStatusUpdate update)
		{
			if (update.OrderId is not null)
			{
				return _unitOfWork.OrderHeader.Get(order => order.Id == update.OrderId.Value, tracked: true);
			}

			return _unitOfWork.OrderHeader.Get(order => order.SessionId == update.SessionId, tracked: true);
		}

		private static bool SessionMatches(OrderHeader orderHeader, PaymentSessionStatusUpdate update)
		{
			return !string.IsNullOrWhiteSpace(orderHeader.SessionId) &&
				string.Equals(orderHeader.SessionId, update.SessionId, StringComparison.Ordinal);
		}

		private static bool IsPayable(OrderHeader orderHeader)
		{
			if (orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment)
			{
				return true;
			}

			return orderHeader.OrderStatus == SD.StatusPending &&
				orderHeader.PaymentStatus == SD.PaymentStatusPending;
		}

		private static bool IsTerminal(OrderHeader orderHeader)
		{
			return orderHeader.OrderStatus is SD.StatusCancelled or SD.StatusRefunded ||
				orderHeader.PaymentStatus is SD.StatusCancelled or SD.StatusRefunded or SD.PaymentStatusRejected;
		}
	}
}
