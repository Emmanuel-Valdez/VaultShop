using System.Security.Claims;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Models.ViewModels;
using VaultShop.Utility;

namespace VaultShop.Web.Services.Billing
{
	public sealed class OrderSummaryService : IOrderSummaryService
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly OrderAccessPolicy _orderAccessPolicy;

		public OrderSummaryService(IUnitOfWork unitOfWork, OrderAccessPolicy orderAccessPolicy)
		{
			_unitOfWork = unitOfWork;
			_orderAccessPolicy = orderAccessPolicy;
		}

		public OrderSummaryViewModel? GetSummary(int orderId, ClaimsPrincipal user)
		{
			var orderHeader = _unitOfWork.OrderHeader.Get(
				o => o.Id == orderId,
				includeProperties: "ApplicationUser,Company");

			if (orderHeader == null || !_orderAccessPolicy.CanAccess(orderHeader, user))
				return null;

		var orderDetails = _unitOfWork.OrderDetail.GetAll(
			d => d.OrderHeaderId == orderId,
			includeProperties: "Product")
			.OrderBy(d => d.Id);

			return MapToViewModel(orderHeader, orderDetails);
		}

		private static OrderSummaryViewModel MapToViewModel(OrderHeader o, IEnumerable<OrderDetail> details)
		{
			return new OrderSummaryViewModel
			{
				OrderId = o.Id,
				OrderDate = o.OrderDate,
				OrderStatus = o.OrderStatus,
				PaymentStatus = o.PaymentStatus,
				PaymentMethod = o.PaymentMethod,
				PaymentDueDate = o.PaymentDueDate,

				CustomerName = o.ApplicationUser?.Name ?? string.Empty,

				CompanyName = o.Company?.Name,
				RazonSocial = o.RazonSocialSnapshot,
				DomicilioFiscal = o.DomicilioFiscalSnapshot,
				Cuit = o.CuitSnapshot,

				ShippingName = o.Name,
				ShippingStreetAddress = o.StreetAddress,
				ShippingCity = o.City,
				ShippingState = o.State,
				ShippingPostalCode = o.PostalCode,
				ShippingPhoneNumber = o.PhoneNumber,

				DeliveryType = o.DeliveryType,
				PickupAgencyCode = o.PickupAgencyCode,
				PickupAgencyName = o.PickupAgencyName,
				PickupAgencyAddress = o.PickupAgencyAddress,
				PickupAgencyHours = o.PickupAgencyHours,

				Items = details.Select(d => new OrderSummaryItemViewModel
				{
					ProductName = d.Product?.Name ?? string.Empty,
					UnitPrice = d.Price,
					Quantity = d.Count,
				}).ToList(),

				OrderTotal = o.OrderTotal,
			};
		}
	}
}
