using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models;
using UkiyoDesigns.Utility;

namespace UkiyoDesignsWeb.Services.Payments
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
	}
}
