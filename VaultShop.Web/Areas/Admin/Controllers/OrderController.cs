using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using System.Security.Claims;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Models.ViewModels;
using VaultShop.Utility;
using VaultShop.Web.Services.Email;
using VaultShop.Web.Services.Payments;

namespace VaultShop.Web.Areas.Admin.Controllers
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
		private readonly IPaymentRefundService _paymentRefundService;
		private readonly IPaymentStatusService _paymentStatusService;
		private readonly IWebHostEnvironment _environment;
		private readonly IConfiguration _configuration;
		private readonly ITransactionalEmailService _emailService;

		public OrderController(IUnitOfWork unitOfWork, IStringLocalizer<OrderController> localizer, ILogger<OrderController> logger, 
			IPaymentSessionService paymentSessionService, IPaymentRefundService paymentRefundService, IPaymentStatusService paymentStatusService,
			IWebHostEnvironment environment, IConfiguration configuration, ITransactionalEmailService emailService)
		{
			_unitOfWork = unitOfWork;
			_localizer = localizer;
			_logger = logger;
			_paymentSessionService = paymentSessionService;
			_paymentRefundService = paymentRefundService;
			_paymentStatusService = paymentStatusService;
			_environment = environment;
			_configuration = configuration;
			_emailService = emailService;
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
			ViewData["AllowDevelopmentManualPaymentApproval"] = ManualPaymentApprovalEnabled();
			PopulateBankTransferViewData();
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
			if (IsTerminal(orderHeaderFromDb))
			{
				_logger.LogWarning("Rejected order-detail update for terminal order {OrderId}. OrderStatus: {OrderStatus}. PaymentStatus: {PaymentStatus}.", orderHeaderFromDb.Id, orderHeaderFromDb.OrderStatus, orderHeaderFromDb.PaymentStatus);
				return RedirectToAction(nameof(Details), new { orderId = orderHeaderFromDb.Id });
			}

			if (orderHeaderFromDb.OrderStatus == SD.StatusShipped && !User.IsInRole(SD.Role_Admin))
			{
				_logger.LogWarning("Rejected shipped order-detail update by non-admin for order {OrderId}.", orderHeaderFromDb.Id);
				return RedirectToAction(nameof(Details), new { orderId = orderHeaderFromDb.Id });
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
			if (OrderVM == null || OrderVM.OrderHeader.Id <= 0)
			{
				return NotFound();
			}

			var orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVM.OrderHeader.Id);
			if (orderHeader == null)
			{
				return NotFound();
			}
			if (!CanStartProcessing(orderHeader))
			{
				_logger.LogWarning("Rejected start-processing request for unpaid or non-approved order {OrderId}. OrderStatus: {OrderStatus}. PaymentStatus: {PaymentStatus}.", orderHeader.Id, orderHeader.OrderStatus, orderHeader.PaymentStatus);
				return RedirectToAction(nameof(Details), new { orderId = orderHeader.Id });
			}

			_unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, SD.StatusInProcess);
			_unitOfWork.Save();
			_logger.LogInformation("Marked order {OrderId} as in process.", orderHeader.Id);
			TempData["Success"] = _localizer["OrderDetailsUpdatedSuccessfully"].Value;
			return RedirectToAction(nameof(Details), new { orderId = orderHeader.Id });
		}

		[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
		[HttpPost]
		public async Task<IActionResult> ShipOrder()
		{
			var orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVM.OrderHeader.Id);
			if (orderHeader == null)
			{
				return NotFound();
			}
			if (!CanShipOrder(orderHeader))
			{
				_logger.LogWarning("Rejected ship-order request for unpaid or non-processing order {OrderId}. OrderStatus: {OrderStatus}. PaymentStatus: {PaymentStatus}.", orderHeader.Id, orderHeader.OrderStatus, orderHeader.PaymentStatus);
				return RedirectToAction(nameof(Details), new { orderId = orderHeader.Id });
			}

			orderHeader.TrackingNumber = OrderVM.OrderHeader.TrackingNumber;
			orderHeader.Carrier = OrderVM.OrderHeader.Carrier;
			orderHeader.OrderStatus = SD.StatusShipped;
			orderHeader.ShippingDate = DateTime.UtcNow;

			_unitOfWork.OrderHeader.Update(orderHeader);
			_unitOfWork.Save();
			_logger.LogInformation("Marked order {OrderId} as shipped. DelayedPayment: {IsDelayedPayment}", orderHeader.Id, orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment);
			
			await _emailService.TrySendShippingConfirmationAsync(orderHeader.Id);
			
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
				if (orderHeader.PaymentMethod == SD.PaymentMethodStripe && !string.IsNullOrWhiteSpace(orderHeader.PaymentIntentId))
				{
					try
					{
						_paymentRefundService.RefundPaymentIntent(orderHeader.PaymentIntentId);
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
					_logger.LogWarning("Cancelled paid order {OrderId} without automated Stripe refund. PaymentMethod: {PaymentMethod}, HasPaymentIntent: {HasPaymentIntent}. Manual refund review required.", orderHeader.Id, orderHeader.PaymentMethod, !string.IsNullOrWhiteSpace(orderHeader.PaymentIntentId));
					_unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, SD.StatusCancelled);
				}
			}
			else
			{
				TryExpireCheckoutSession(orderHeader);
				_unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, SD.StatusCancelled, SD.StatusCancelled);
			}
			_unitOfWork.Save();
			_logger.LogInformation("Cancelled order {OrderId}. PreviousPaymentStatus: {PaymentStatus}", orderHeader.Id, orderHeader.PaymentStatus);
			TempData["Success"] = _localizer["OrderCancelledSuccessfully"].Value;
			return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
		}
		[ActionName("Details")]
		[HttpPost]
		public IActionResult Details_PAY_NOW(int orderId, string? paymentMethod)
		{
			var orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == orderId);
			if (orderHeader == null)
			{
				return NotFound();
			}
			if (!UserCanAccessOrder(orderHeader))
			{
				return NotFound();
			}
			if (orderHeader.CompanyId.GetValueOrDefault() == 0 ||
				orderHeader.PaymentStatus != SD.PaymentStatusDelayedPayment ||
				IsTerminal(orderHeader))
			{
				return NotFound();
			}

			OrderVM.OrderHeader = orderHeader;
			OrderVM.OrderDetail = _unitOfWork.OrderDetail
				.GetAll(u => u.OrderHeaderId == OrderVM.OrderHeader.Id, includeProperties: "Product");

			paymentMethod = string.IsNullOrWhiteSpace(paymentMethod)
				? SD.PaymentMethodStripe
				: paymentMethod.Trim();
			if (paymentMethod != SD.PaymentMethodStripe && paymentMethod != SD.PaymentMethodBankTransfer)
			{
				return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
			}

			if (paymentMethod == SD.PaymentMethodBankTransfer)
			{
				orderHeader.PaymentMethod = SD.PaymentMethodBankTransfer;
				orderHeader.SessionId = string.Empty;
				orderHeader.PaymentIntentId = string.Empty;
				_unitOfWork.OrderHeader.Update(orderHeader);
				_unitOfWork.Save();
				_logger.LogInformation("Company delayed-payment order {OrderId} switched to bank transfer.", orderHeader.Id);
				return RedirectToAction(nameof(Details), new { orderId = orderHeader.Id });
			}

			orderHeader.PaymentMethod = SD.PaymentMethodStripe;
			_unitOfWork.OrderHeader.Update(orderHeader);

			string currentCulture = Thread.CurrentThread.CurrentUICulture.Name;
			var domain = Request.Scheme + "://" + Request.Host.Value + "/";
			PaymentSessionResult session;
			try
			{
				session = _paymentSessionService.CreateCheckoutSession(new PaymentSessionRequest(
					OrderVM.OrderHeader.Id,
					OrderVM.OrderDetail.Select(item => new PaymentSessionLineItem(item.Product.Name, item.Price, item.Count)),
					domain + $"{currentCulture}/admin/order/PaymentConfirmation?orderHeaderId={OrderVM.OrderHeader.Id}&session_id={{CHECKOUT_SESSION_ID}}",
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

		public IActionResult PaymentConfirmation(int orderHeaderId, [FromQuery(Name = "session_id")] string? sessionId)
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
			if (!ConfirmationSessionMatches(orderHeader, sessionId))
			{
				_logger.LogWarning("Rejected payment confirmation for order {OrderId} with session {SessionId}. Stored session: {StoredSessionId}.", orderHeader.Id, sessionId, orderHeader.SessionId);
				return NotFound();
			}

			if (orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment)
			{
				SyncPaidCheckoutSession(orderHeader);
				orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == orderHeaderId) ?? orderHeader;
			}

			return View(orderHeader);
		}

		[Authorize(Roles = SD.Role_Admin)]
		[HttpPost]
		public IActionResult ApprovePaymentDevelopment()
		{
			if (!ManualPaymentApprovalEnabled())
			{
				return NotFound();
			}

			if (OrderVM == null || OrderVM.OrderHeader.Id <= 0)
			{
				return NotFound();
			}

			var orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVM.OrderHeader.Id);
			if (orderHeader == null || string.IsNullOrWhiteSpace(orderHeader.SessionId))
			{
				return NotFound();
			}

			var approved = _paymentStatusService.MarkCheckoutSessionPaid(
				new PaymentSessionStatusUpdate(orderHeader.Id, orderHeader.SessionId, orderHeader.PaymentIntentId));
			if (approved)
			{
				_logger.LogWarning("Development-only manual payment approval used for order {OrderId}.", orderHeader.Id);
			}

			return RedirectToAction(nameof(Details), new { orderId = orderHeader.Id });
		}

		[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
		[HttpPost]
		public async Task<IActionResult> ConfirmBankTransfer()
		{
			if (OrderVM == null || OrderVM.OrderHeader.Id <= 0)
			{
				return NotFound();
			}

			var approved = _paymentStatusService.ApproveManualBankTransfer(OrderVM.OrderHeader.Id);
			if (approved)
			{
				await _emailService.TrySendPaymentReceiptAsync(OrderVM.OrderHeader.Id);
				TempData["Success"] = _localizer["BankTransferConfirmedSuccessfully"].Value;
			}

			return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ConfirmTransferSent(int orderId)
		{
			var orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == orderId);
			if (orderHeader == null)
			{
				return NotFound();
			}
			if (!UserCanAccessOrder(orderHeader))
			{
				return NotFound();
			}

			if (orderHeader.PaymentMethod != SD.PaymentMethodBankTransfer)
			{
				return RedirectToAction(nameof(Details), new { orderId });
			}
			if (orderHeader.PaymentStatus != SD.PaymentStatusPending && orderHeader.PaymentStatus != SD.PaymentStatusDelayedPayment)
			{
				return RedirectToAction(nameof(Details), new { orderId });
			}
			if (!orderHeader.TransferConfirmedByCustomerAt.HasValue)
			{
				orderHeader.TransferConfirmedByCustomerAt = DateTime.UtcNow;
				_unitOfWork.OrderHeader.Update(orderHeader);
				_unitOfWork.Save();
			}

			await _emailService.TrySendAdminBankTransferConfirmationRequestAsync(orderId);

			return RedirectToAction(nameof(Details), new { orderId });
		}

		private void PopulateBankTransferViewData()
		{
			ViewData["StripeEnabled"] = _configuration.GetValue("Payments:StripeEnabled", true);
			ViewData["BankTransferEnabled"] = _configuration.GetValue("Payments:BankTransferEnabled", true);
			ViewData["MercadoPagoEnabled"] = _configuration.GetValue("Payments:MercadoPagoEnabled", false);
			ViewData["BankTransferCbu"] = _configuration.GetValue<string>("Payments:BankTransferCbu") ?? string.Empty;
			ViewData["BankTransferAlias"] = _configuration.GetValue<string>("Payments:BankTransferAlias") ?? string.Empty;
			ViewData["BankTransferRecipientName"] = _configuration.GetValue<string>("Payments:BankTransferRecipientName") ?? string.Empty;
			ViewData["BankTransferBankName"] = _configuration.GetValue<string>("Payments:BankTransferBankName") ?? string.Empty;
		}

		private static bool ConfirmationSessionMatches(OrderHeader orderHeader, string? sessionId)
		{
			return !string.IsNullOrWhiteSpace(orderHeader.SessionId) &&
				string.Equals(orderHeader.SessionId, sessionId, StringComparison.Ordinal);
		}

		private static bool IsTerminal(OrderHeader orderHeader)
		{
			return orderHeader.OrderStatus is SD.StatusCancelled or SD.StatusRefunded ||
				orderHeader.PaymentStatus is SD.StatusCancelled or SD.StatusRefunded or SD.PaymentStatusRejected;
		}

		private static bool CanStartProcessing(OrderHeader orderHeader)
		{
			return orderHeader.OrderStatus == SD.StatusApproved &&
				(orderHeader.PaymentStatus == SD.PaymentStatusApproved ||
					(orderHeader.CompanyId.GetValueOrDefault() > 0 &&
						orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment));
		}

		private static bool CanShipOrder(OrderHeader orderHeader)
		{
			return orderHeader.OrderStatus == SD.StatusInProcess &&
				orderHeader.PaymentStatus == SD.PaymentStatusApproved &&
				!IsTerminal(orderHeader);
		}

		private static string PaymentMethodLabel(string? paymentMethod)
		{
			return paymentMethod switch
			{
				SD.PaymentMethodStripe => SD.PaymentMethodStripe,
				SD.PaymentMethodBankTransfer => SD.PaymentMethodBankTransfer,
				_ => "Unspecified"
			};
		}

		private bool ManualPaymentApprovalEnabled()
		{
			return _environment.IsDevelopment() &&
				_configuration.GetValue<bool>("Payments:AllowDevelopmentManualApproval");
		}

		private void TryExpireCheckoutSession(OrderHeader orderHeader)
		{
			if (string.IsNullOrWhiteSpace(orderHeader.SessionId))
			{
				return;
			}

			try
			{
				_paymentSessionService.ExpireCheckoutSession(orderHeader.SessionId);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Could not expire checkout session {SessionId} while cancelling order {OrderId}.", orderHeader.SessionId, orderHeader.Id);
			}
		}

		private void SyncPaidCheckoutSession(OrderHeader orderHeader)
		{
			if (string.IsNullOrWhiteSpace(orderHeader.SessionId))
			{
				return;
			}

			try
			{
				var session = _paymentSessionService.GetCheckoutSessionStatus(orderHeader.SessionId);
				if (session.IsPaid)
				{
					_paymentStatusService.MarkCheckoutSessionPaid(new PaymentSessionStatusUpdate(orderHeader.Id, session.SessionId, session.PaymentIntentId));
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Could not sync payment status for order {OrderId} from checkout session {SessionId}.", orderHeader.Id, orderHeader.SessionId);
			}
		}
		#region API CALLS

		[HttpGet]

		public IActionResult GetAll(string status)
		{
			IEnumerable<OrderHeader> objOrderHeaders;
			if (User.IsInRole(SD.Role_Admin) || User.IsInRole(SD.Role_Employee))
			{
				objOrderHeaders = _unitOfWork.OrderHeader.GetAll(includeProperties: "ApplicationUser,Company").ToList();
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
					objOrderHeaders = objOrderHeaders.Where(u =>
						u.PaymentStatus == SD.PaymentStatusPending ||
						u.PaymentStatus == SD.PaymentStatusDelayedPayment);
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
			var data = objOrderHeaders
				.OrderByDescending(u => u.OrderDate)
				.ThenByDescending(u => u.Id)
				.Select(u => new
				{
					id = u.Id,
					name = u.Name,
					phoneNumber = u.PhoneNumber,
					applicationUser = new { email = u.ApplicationUser?.Email ?? string.Empty },
					company = u.Company == null ? null : new { name = u.Company.Name },
					orderStatus = u.OrderStatus,
					paymentStatus = u.PaymentStatus,
					paymentMethod = PaymentMethodLabel(u.PaymentMethod),
					orderTotal = u.OrderTotal
				});
			return Json(new { data });

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
