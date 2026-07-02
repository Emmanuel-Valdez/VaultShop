using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Utility;

namespace VaultShop.Web.Services.Payments
{
	public sealed class PaymentStatusService : IPaymentStatusService
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly ILogger<PaymentStatusService> _logger;

		public PaymentStatusService(IUnitOfWork unitOfWork, ILogger<PaymentStatusService> logger)
		{
			_unitOfWork = unitOfWork;
			_logger = logger;
		}

		public bool MarkCheckoutSessionPaid(PaymentSessionStatusUpdate update)
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
				? orderHeader.OrderStatus ?? SD.StatusApproved
				: SD.StatusApproved;
			_unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, nextOrderStatus, SD.PaymentStatusApproved);
			_unitOfWork.Save();
			_logger.LogInformation("Marked order {OrderId} as paid from checkout session {SessionId}.", orderHeader.Id, update.SessionId);
			return true;
		}

		public bool MarkCheckoutSessionFailed(PaymentSessionStatusUpdate update)
		{
			var orderHeader = FindOrder(update);
			if (orderHeader == null)
			{
				_logger.LogWarning("Could not find order for failed checkout session {SessionId}.", update.SessionId);
				return false;
			}

			_unitOfWork.OrderHeader.UpdateStripePaymentId(orderHeader.Id, update.SessionId, update.PaymentIntentId ?? string.Empty);
			_unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, SD.StatusCancelled, SD.PaymentStatusRejected);
			_unitOfWork.Save();
			_logger.LogInformation("Marked order {OrderId} as payment failed from checkout session {SessionId}.", orderHeader.Id, update.SessionId);
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
