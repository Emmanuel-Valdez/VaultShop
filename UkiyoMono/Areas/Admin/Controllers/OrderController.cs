using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Stripe;
using System.Security.Claims;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models;
using UkiyoDesigns.Models.ViewModels;
using UkiyoDesigns.Utility;
using UkiyoDesignsWeb.Services.Payments;

namespace UkiyoDesignsWeb.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize]
	public class OrderController : Controller
	{
		private readonly IUnitOfWork _unitOfWork;
		[BindProperty]
		public OrderVM OrderVM { get; set; } = null!;
		private readonly IStringLocalizer<OrderController> _localizer;
		private readonly ILogger<OrderController> _logger;
		private readonly IPaymentSessionService _paymentSessionService;

		public OrderController(IUnitOfWork unitOfWork, IStringLocalizer<OrderController> localizer, ILogger<OrderController> logger, IPaymentSessionService paymentSessionService)
		{
			_unitOfWork = unitOfWork;
			_localizer = localizer;
			_logger = logger;
			_paymentSessionService = paymentSessionService;
		}

		public IActionResult Index()
		{
			return View();
		}

		public IActionResult Details(int orderId)
		{
			var orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == orderId, includeProperties: "ApplicationUser");
			if (orderHeader == null)
			{
				return NotFound();
			}
			if (!UserCanAccessOrder(orderHeader))
			{
				return NotFound();
			}

			OrderVM = new()
			{
				OrderHeader = orderHeader,
				OrderDetail = _unitOfWork.OrderDetail.GetAll(u => u.OrderHeaderId == orderId, includeProperties: "Product")
			};
			return View(OrderVM);
		}

		[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
		[HttpPost]
		public IActionResult UpdateOrderDetail()
		{
			if (User.IsInRole(SD.Role_Admin) && !ModelState.IsValid)
				return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });

			var orderHeaderFromDb = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVM.OrderHeader.Id);
			if (orderHeaderFromDb == null)
			{
				return NotFound();
			}

			if (User.IsInRole(SD.Role_Admin))
			{
				orderHeaderFromDb.Name = OrderVM.OrderHeader.Name;
				orderHeaderFromDb.PhoneNumber = OrderVM.OrderHeader.PhoneNumber;
				orderHeaderFromDb.StreetAddress = OrderVM.OrderHeader.StreetAddress;
				orderHeaderFromDb.City = OrderVM.OrderHeader.City;
				orderHeaderFromDb.State = OrderVM.OrderHeader.State;
				orderHeaderFromDb.PostalCode = OrderVM.OrderHeader.PostalCode;
			}
			if (!string.IsNullOrEmpty(OrderVM.OrderHeader.Carrier))
			{
				orderHeaderFromDb.Carrier = OrderVM.OrderHeader.Carrier;
			}
			if (!string.IsNullOrEmpty(OrderVM.OrderHeader.TrackingNumber))
			{
				orderHeaderFromDb.TrackingNumber = OrderVM.OrderHeader.TrackingNumber;
			}
			_unitOfWork.OrderHeader.Update(orderHeaderFromDb);
			_unitOfWork.Save();
			_logger.LogInformation("Updated order details for order {OrderId}.", orderHeaderFromDb.Id);
			TempData["Success"] = _localizer["OrderDetailsUpdatedSuccessfully"].Value;
			return RedirectToAction(nameof(Details), new { orderId = orderHeaderFromDb.Id });

		}
		[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
		[HttpPost]
		public IActionResult StartProcessing()
		{
			_unitOfWork.OrderHeader.UpdateStatus(OrderVM.OrderHeader.Id, SD.StatusInProcess);
			_unitOfWork.Save();
			_logger.LogInformation("Marked order {OrderId} as in process.", OrderVM.OrderHeader.Id);
			TempData["Success"] = _localizer["OrderDetailsUpdatedSuccessfully"].Value;
			return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
		}

		[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
		[HttpPost]
		public IActionResult ShipOrder()
		{
			var orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVM.OrderHeader.Id);
			if (orderHeader == null)
			{
				return NotFound();
			}

			orderHeader.TrackingNumber = OrderVM.OrderHeader.TrackingNumber;
			orderHeader.Carrier = OrderVM.OrderHeader.Carrier;
			orderHeader.OrderStatus = SD.StatusShipped;
			var shippedAt = DateTime.UtcNow;
			orderHeader.ShippingDate = shippedAt;
			if (orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment)
			{
				orderHeader.PaymentDueDate = DateOnly.FromDateTime(shippedAt.AddDays(7));
			}

			_unitOfWork.OrderHeader.Update(orderHeader);
			_unitOfWork.Save();
			_logger.LogInformation("Marked order {OrderId} as shipped. DelayedPayment: {IsDelayedPayment}", orderHeader.Id, orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment);
			TempData["Success"] = _localizer["OrderShippedSuccessfully"].Value;
			return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
		}

		[Authorize(Roles = SD.Role_Admin)]
		[HttpPost]
		public IActionResult CancelOrder()
		{
			var orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVM.OrderHeader.Id);
			if (orderHeader == null)
			{
				return NotFound();
			}

			if (orderHeader.PaymentStatus == SD.PaymentStatusApproved)
			{
				var options = new RefundCreateOptions
				{
					Reason = RefundReasons.RequestedByCustomer,
					PaymentIntent = orderHeader.PaymentIntentId
				};
				var service = new RefundService();
				try
				{
					Refund refund = service.Create(options);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to create Stripe refund while cancelling order {OrderId}.", orderHeader.Id);
					throw;
				}
				_unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, SD.StatusCancelled, SD.StatusRefunded);
			}
			else
			{
				_unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, SD.StatusCancelled, SD.StatusCancelled);
			}
			_unitOfWork.Save();
			_logger.LogInformation("Cancelled order {OrderId}. PreviousPaymentStatus: {PaymentStatus}", orderHeader.Id, orderHeader.PaymentStatus);
			TempData["Success"] = _localizer["OrderCancelledSuccessfully"].Value;
			return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
		}
		[ActionName("Details")]
		[HttpPost]
		public IActionResult Details_PAY_NOW(int orderId)
		{
			var orderHeader = _unitOfWork.OrderHeader
				.Get(u => u.Id == orderId, includeProperties: "ApplicationUser");
			if (orderHeader == null)
			{
				return NotFound();
			}
			if (!UserCanAccessOrder(orderHeader))
			{
				return NotFound();
			}

			OrderVM.OrderHeader = orderHeader;
			OrderVM.OrderDetail = _unitOfWork.OrderDetail
				.GetAll(u => u.OrderHeaderId == OrderVM.OrderHeader.Id, includeProperties: "Product");

			string currentCulture = Thread.CurrentThread.CurrentUICulture.Name;
			var domain = Request.Scheme + "://" + Request.Host.Value + "/";
			PaymentSessionResult session;
			try
			{
				session = _paymentSessionService.CreateCheckoutSession(new PaymentSessionRequest(
					OrderVM.OrderHeader.Id,
					OrderVM.OrderDetail.Select(item => new PaymentSessionLineItem(item.Product.Name, item.Price, item.Count)),
					domain + $"{currentCulture}/admin/order/PaymentConfirmation?orderHeaderId={OrderVM.OrderHeader.Id}",
					domain + $"{currentCulture}/admin/order/details?orderId={OrderVM.OrderHeader.Id}"));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to create payment session for delayed-payment order {OrderId}.", OrderVM.OrderHeader.Id);
				throw;
			}
			_unitOfWork.OrderHeader.UpdateStripePaymentId(OrderVM.OrderHeader.Id, session.SessionId, session.PaymentIntentId ?? string.Empty);
			_unitOfWork.Save();
			_logger.LogInformation("Created payment session for delayed-payment order {OrderId}.", OrderVM.OrderHeader.Id);
			Response.Headers["Location"] = session.Url;
			return new StatusCodeResult(303);

		}

		public IActionResult PaymentConfirmation(int orderHeaderId)
		{
			OrderHeader? orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == orderHeaderId);
			if (orderHeader == null)
			{
				return NotFound();
			}
			if (!UserCanAccessOrder(orderHeader))
			{
				return NotFound();
			}

			return View(orderHeaderId);
		}
		#region API CALLS

		[HttpGet]

		public IActionResult GetAll(string status)
		{
			IEnumerable<OrderHeader> objOrderHeaders;
			if (User.IsInRole(SD.Role_Admin) || User.IsInRole(SD.Role_Employee))
			{
				objOrderHeaders = _unitOfWork.OrderHeader.GetAll(includeProperties: "ApplicationUser").ToList();
			}
			else
			{
				var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
				if (string.IsNullOrEmpty(userId))
				{
					return Unauthorized();
				}

				var currentUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId);
				if (currentUser?.CompanyId.GetValueOrDefault() > 0)
				{
					objOrderHeaders = _unitOfWork.OrderHeader
						.GetAll(u => u.CompanyId == currentUser.CompanyId, includeProperties: "ApplicationUser");
				}
				else
				{
					objOrderHeaders = _unitOfWork.OrderHeader
						.GetAll(u => u.ApplicationUserId == userId, includeProperties: "ApplicationUser");
				}
			}

			switch (status)
			{
				case "pending":
					objOrderHeaders = objOrderHeaders.Where(u => u.PaymentStatus == SD.PaymentStatusDelayedPayment);
					break;
				case "inprocess":
					objOrderHeaders = objOrderHeaders.Where(u => u.OrderStatus == SD.StatusInProcess);
					break;
				case "completed":
					objOrderHeaders = objOrderHeaders.Where(u => u.OrderStatus == SD.StatusShipped);
					break;
				case "approved":
					objOrderHeaders = objOrderHeaders.Where(u => u.OrderStatus == SD.StatusApproved);
					break;
				default:
					break;
			}
			return Json(new { data = objOrderHeaders });

		}
		#endregion

		private bool UserCanAccessOrder(OrderHeader orderHeader)
		{
			if (User.IsInRole(SD.Role_Admin) || User.IsInRole(SD.Role_Employee))
			{
				return true;
			}

			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (string.IsNullOrEmpty(userId))
			{
				return false;
			}

			if (orderHeader.ApplicationUserId == userId && orderHeader.CompanyId.GetValueOrDefault() == 0)
			{
				return true;
			}

			var currentUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId);
			if (currentUser == null || currentUser.CompanyId.GetValueOrDefault() <= 0)
			{
				return false;
			}

			return orderHeader.CompanyId == currentUser.CompanyId;
		}
	}
}
