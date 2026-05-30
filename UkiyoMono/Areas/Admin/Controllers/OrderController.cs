using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Stripe;
using Stripe.Checkout;
using System.CodeDom;
using System.Diagnostics;
using System.Runtime.ConstrainedExecution;
using System.Security.Claims;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models;
using UkiyoDesigns.Models.ViewModels;
using UkiyoDesigns.Utility;

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

		public OrderController(IUnitOfWork unitOfWork, IStringLocalizer<OrderController> localizer)
		{
			_unitOfWork = unitOfWork;
			_localizer = localizer;
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
			TempData["Success"] = _localizer["OrderDetailsUpdatedSuccessfully"].Value;
			return RedirectToAction(nameof(Details), new { orderId = orderHeaderFromDb.Id });

		}
		[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
		[HttpPost]
		public IActionResult StartProcessing()
		{
			_unitOfWork.OrderHeader.UpdateStatus(OrderVM.OrderHeader.Id, SD.StatusInProcess);
			_unitOfWork.Save();
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
			orderHeader.ShippingDate = DateTime.Now;
			if (orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment)
			{
				orderHeader.PaymentDueDate = DateOnly.FromDateTime(DateTime.Now.AddDays(7));
			}

			_unitOfWork.OrderHeader.Update(orderHeader);
			_unitOfWork.Save();
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
				Refund refund = service.Create(options);
				_unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, SD.StatusCancelled, SD.StatusRefunded);
			}
			else
			{
				_unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, SD.StatusCancelled, SD.StatusCancelled);
			}
			_unitOfWork.Save();
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

			//stripe logic
			string currentCulture = Thread.CurrentThread.CurrentUICulture.Name;
			var domain = Request.Scheme + "://" + Request.Host.Value + "/";
			var options = new SessionCreateOptions
			{
				SuccessUrl = domain + $"{currentCulture}/admin/order/PaymentConfirmation?orderHeaderId={OrderVM.OrderHeader.Id}",
				CancelUrl = domain + $"{currentCulture}/admin/order/details?orderId={OrderVM.OrderHeader.Id}",
				LineItems = new List<SessionLineItemOptions>(),
				Mode = "payment",
			};
			foreach (var item in OrderVM.OrderDetail)
			{
				var sessionLineItem = new SessionLineItemOptions
				{
					PriceData = new SessionLineItemPriceDataOptions
					{
						UnitAmount = (long)(item.Price * 100),
						Currency = "usd",
						ProductData = new SessionLineItemPriceDataProductDataOptions
						{
							Name = item.Product.Name
						}
					},
					Quantity = item.Count
				};
				options.LineItems.Add(sessionLineItem);
			}
			var service = new SessionService();
			Session session = service.Create(options);
			_unitOfWork.OrderHeader.UpdateStripePaymentId(OrderVM.OrderHeader.Id, session.Id, session.PaymentIntentId);
			_unitOfWork.Save();
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

			if (orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment)
			{
				// order made by company
				var service = new SessionService();
				Session session = service.Get(orderHeader.SessionId);
				if (session.PaymentStatus.ToLower() == "paid")
				{
					_unitOfWork.OrderHeader.UpdateStripePaymentId(orderHeaderId, session.Id, session.PaymentIntentId);
					_unitOfWork.OrderHeader.UpdateStatus(orderHeaderId, orderHeader.OrderStatus ?? SD.StatusApproved, SD.PaymentStatusApproved);
					_unitOfWork.Save();
				}
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
