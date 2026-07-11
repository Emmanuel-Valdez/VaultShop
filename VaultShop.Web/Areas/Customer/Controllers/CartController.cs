using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using System.Globalization;
using System.Security.Claims;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Models.ViewModels;
using VaultShop.Utility;
using VaultShop.Web.Services.Checkout;
using VaultShop.Web.Services.Email;
using VaultShop.Web.Services.Payments;

namespace VaultShop.Web.Areas.Customer.Controllers
{
	[Area("Customer")]
	[Authorize]
	public class CartController : Controller
	{
		private readonly IUnitOfWork _unitOfWork;
		[BindProperty]
		public ShoppingCartVM ShoppingCartVM { get; set; } = null!;
		private readonly IStringLocalizer<CartController> _localizer;
		private readonly SignInManager<ApplicationUser> _signInManager;
		private readonly ILogger<CartController> _logger;
		private readonly ICheckoutService _checkoutService;
		private readonly IPaymentSessionService _paymentSessionService;
		private readonly IPaymentStatusService _paymentStatusService;
		private readonly ITransactionalEmailService _emailService;
		private readonly IConfiguration _configuration;
		public CartController(IUnitOfWork unitOfWork,IStringLocalizer<CartController> localizer, SignInManager<ApplicationUser> signInManager,
			ILogger<CartController> logger, ICheckoutService checkoutService, IPaymentSessionService paymentSessionService,
			IPaymentStatusService paymentStatusService, ITransactionalEmailService emailService, IConfiguration configuration)
		{
			_localizer = localizer;
			_unitOfWork = unitOfWork;
			_signInManager = signInManager;
			_logger = logger;
			_checkoutService = checkoutService;
			_paymentSessionService = paymentSessionService;
			_paymentStatusService = paymentStatusService;
			_emailService = emailService;
			_configuration = configuration;
		}
		public IActionResult Index()
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized();
			}
			ShoppingCartVM = new()
			{
				ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId , includeProperties: "Product.Category,Product.ProductImages"),
				OrderHeader = new()
			};
			RemoveShoppingCartsOutdated(userId);
			foreach (var cart in ShoppingCartVM.ShoppingCartList)
			{
				cart.Price = GetPriceBasedOnRole(cart);
				ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
			}
		

			return View(ShoppingCartVM);
		}
		private void RemoveShoppingCartsOutdated(string userId)
		{
			foreach (var cart in ShoppingCartVM.ShoppingCartList)
			{
				if (cart.Product.IsAvailableInStore == false || cart.Product.IsDeleted == true)
				{
					HttpContext.Session.SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart
						.GetAll(u => u.ApplicationUserId == cart.ApplicationUserId).Count() - 1);
					_unitOfWork.ShoppingCart.Remove(cart);
				}
			}
			_unitOfWork.Save();
			ShoppingCartVM.ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId, includeProperties: "Product.Category,Product.ProductImages");
			HttpContext.Session.SetInt32(SD.SessionCart, ShoppingCartVM.ShoppingCartList.Count());

		}

		public async Task<IActionResult> Summary()
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized();
			}

			var result = _checkoutService.BuildSummary(userId, User.IsInRole(SD.Role_Company));

			if (!result.IsAuthorized)
			{
				return Unauthorized();
			}
			if (result.ShouldBlockUser && result.ApplicationUser is not null)
			{
				return await ClearCartAndBlockUser(result.ApplicationUser);
			}
			if (result.IsCartEmpty)
			{
				TempData["error"] = _localizer["CartEmptyOrInvalidError"].Value;
				return RedirectToAction(nameof(Index));
			}
			ViewData["StripeEnabled"] = _configuration.GetValue("Payments:StripeEnabled", true);
			ViewData["BankTransferEnabled"] = _configuration.GetValue("Payments:BankTransferEnabled", true);
			ViewData["BankTransferCbu"] = _configuration.GetValue<string>("Payments:BankTransferCbu") ?? string.Empty;
			ViewData["BankTransferAlias"] = _configuration.GetValue<string>("Payments:BankTransferAlias") ?? string.Empty;
			ViewData["BankTransferRecipientName"] = _configuration.GetValue<string>("Payments:BankTransferRecipientName") ?? string.Empty;
			ViewData["BankTransferBankName"] = _configuration.GetValue<string>("Payments:BankTransferBankName") ?? string.Empty;
			return View(result.ShoppingCartVM);
		}

		[HttpPost]
		[ActionName("Summary")]
		public async Task<IActionResult> SummaryPOST()
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized();
			}

			if (!ModelState.IsValid)
			{
				var summaryResult = _checkoutService.BuildSummary(userId, User.IsInRole(SD.Role_Company));
				return View(summaryResult.ShoppingCartVM ?? ShoppingCartVM);
			}

			var isCompanyCheckout = User.IsInRole(SD.Role_Company);
			if (isCompanyCheckout)
			{
				ShoppingCartVM.OrderHeader.PaymentMethod = null;
			}
			else
			{
				var allowedPaymentMethods = new List<string>();
				if (_configuration.GetValue("Payments:StripeEnabled", true)) allowedPaymentMethods.Add(SD.PaymentMethodStripe);
				if (_configuration.GetValue("Payments:BankTransferEnabled", true)) allowedPaymentMethods.Add(SD.PaymentMethodBankTransfer);

				if (ShoppingCartVM.OrderHeader.PaymentMethod is not string paymentMethod || !allowedPaymentMethods.Contains(paymentMethod))
				{
					TempData["error"] = _localizer["InvalidPaymentMethodError"].Value;
					return RedirectToAction(nameof(Index));
				}
			}

			var result = _checkoutService.CreateOrder(userId, ShoppingCartVM.OrderHeader, isCompanyCheckout);

			if (!result.IsAuthorized)
			{
				return Unauthorized();
			}
			if (result.ShouldBlockUser && result.ApplicationUser is not null)
			{
				return await ClearCartAndBlockUser(result.ApplicationUser);
			}
			if (result.IsCartEmpty)
			{
				TempData["error"] = _localizer["CartEmptyOrInvalidError"].Value;
				HttpContext.Session.SetInt32(SD.SessionCart, 0);
				return RedirectToAction(nameof(Index));
			}
			if (result.OrderTotalInvalid)
			{
				TempData["error"] = _localizer["OrderTotalZeroError"].Value;
				return RedirectToAction(nameof(Index));
			}

			if (result.OrderId is null || result.ShoppingCartVM is null)
			{
				throw new InvalidOperationException("Checkout order creation succeeded without returning order details.");
			}

			var orderId = result.OrderId.Value;

			// ponytail: fire emails after order creation � failure is logged, doesn't block the flow
			await _emailService.TrySendOrderConfirmationAsync(orderId);
			await _emailService.TrySendAdminNewOrderAlertAsync(orderId);

			if (result.RequiresOnlinePayment)
			{
				var culture = CultureInfo.CurrentCulture.Name;
				var domain = Request.Scheme + "://" + Request.Host.Value + "/" + culture + "/";
				PaymentSessionResult session;
				try
				{
					session = _paymentSessionService.CreateCheckoutSession(new PaymentSessionRequest(
						orderId,
						result.ShoppingCartVM.ShoppingCartList.Select(item => new PaymentSessionLineItem(item.Product.Name, item.Price, item.Count)),
						domain + $"customer/cart/OrderConfirmation?id={orderId}&session_id={{CHECKOUT_SESSION_ID}}",
						domain + "customer/cart/index"));
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to create payment session for customer order {OrderId}.", orderId);
					throw;
				}
				_unitOfWork.OrderHeader.UpdateStripePaymentId(orderId, session.SessionId, session.PaymentIntentId ?? string.Empty);
				_unitOfWork.Save();
				_logger.LogInformation("Created payment session for customer order {OrderId}.", orderId);
				Response.Headers["Location"] = session.Url;
				return new StatusCodeResult(303);
			}

			ClearShoppingCart(userId);
			return RedirectToAction(nameof(OrderConfirmation), new { id = orderId });
		}

		public IActionResult OrderConfirmation(int id, [FromQuery(Name = "session_id")] string? sessionId)
		{
			OrderHeader? orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == id, includeProperties: "ApplicationUser");
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
				_logger.LogWarning("Rejected order confirmation for order {OrderId} with session {SessionId}. Stored session: {StoredSessionId}.", orderHeader.Id, sessionId, orderHeader.SessionId);
				return NotFound();
			}

			if (orderHeader.OrderStatus == SD.StatusPending && orderHeader.PaymentStatus == SD.PaymentStatusPending)
			{
				SyncPaidCheckoutSession(orderHeader);
				orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == id, includeProperties: "ApplicationUser") ?? orderHeader;
			}

			if (orderHeader.PaymentStatus is SD.PaymentStatusApproved or SD.PaymentStatusDelayedPayment)
			{
				HttpContext.Session.Clear();
				var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
				if (!string.IsNullOrEmpty(userId) && orderHeader.ApplicationUserId == userId)
				{
					List<ShoppingCart> shoppingCarts = _unitOfWork.ShoppingCart
						.GetAll(u => u.ApplicationUserId == orderHeader.ApplicationUserId).ToList();
					_unitOfWork.ShoppingCart.RemoveRange(shoppingCarts);
					_unitOfWork.Save();
				}
			}
			return View(orderHeader);
		}

		private static bool ConfirmationSessionMatches(OrderHeader orderHeader, string? sessionId)
		{
			return string.IsNullOrWhiteSpace(orderHeader.SessionId) ||
				string.Equals(orderHeader.SessionId, sessionId, StringComparison.Ordinal);
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
			return currentUser?.CompanyId.GetValueOrDefault() > 0 && orderHeader.CompanyId == currentUser.CompanyId;
		}

		[HttpPost]
		public IActionResult Plus(int cartId)
		{
			var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId);
			if (cartFromDb == null)
			{
				return NotFound();
			}
			if (!UserCanAccessCart(cartFromDb))
			{
				return NotFound();
			}

			cartFromDb.Count += 1;
			_unitOfWork.ShoppingCart.Update(cartFromDb);
			_unitOfWork.Save();
			return RedirectToAction(nameof(Index));

		}
		[HttpPost]
		public IActionResult Minus(int cartId)
		{
			var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId, tracked: true);
			if (cartFromDb == null)
			{
				return NotFound();
			}
			if (!UserCanAccessCart(cartFromDb))
			{
				return NotFound();
			}

			if (cartFromDb.Count <= 1)
			{
				HttpContext.Session.SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart
				.GetAll(u => u.ApplicationUserId == cartFromDb.ApplicationUserId).Count() - 1);
				_unitOfWork.ShoppingCart.Remove(cartFromDb);
			}
			else
			{
				cartFromDb.Count -= 1;
				_unitOfWork.ShoppingCart.Update(cartFromDb);
			}
			_unitOfWork.Save();
			return RedirectToAction(nameof(Index));
		}

		[HttpPost]
		public IActionResult Remove(int cartId)
		{
			var cartFromDb = _unitOfWork.ShoppingCart.Get(u => u.Id == cartId, tracked: true);
			if (cartFromDb == null)
			{
				return NotFound();
			}
			if (!UserCanAccessCart(cartFromDb))
			{
				return NotFound();
			}

			HttpContext.Session.SetInt32(SD.SessionCart, _unitOfWork.ShoppingCart
				.GetAll(u => u.ApplicationUserId == cartFromDb.ApplicationUserId).Count() - 1);
			_unitOfWork.ShoppingCart.Remove(cartFromDb);
			_unitOfWork.Save();

			return RedirectToAction(nameof(Index));
		}

		private bool UserCanAccessCart(ShoppingCart shoppingCart)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			return !string.IsNullOrEmpty(userId) && shoppingCart.ApplicationUserId == userId;
		}

		private bool HasDeletedCompany(ApplicationUser applicationUser)
		{
			int companyId = applicationUser.CompanyId.GetValueOrDefault();
			return companyId > 0 && _unitOfWork.Company.Get(u => u.Id == companyId && u.IsDeleted == false) == null;
		}

		private void ClearShoppingCart(string userId)
		{
			var shoppingCarts = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId).ToList();
			if (shoppingCarts.Any())
			{
				_unitOfWork.ShoppingCart.RemoveRange(shoppingCarts);
				_unitOfWork.Save();
			}
			HttpContext.Session.SetInt32(SD.SessionCart, 0);
		}

		private async Task<IActionResult> ClearCartAndBlockUser(ApplicationUser applicationUser)
		{
			var shoppingCarts = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == applicationUser.Id).ToList();
			_logger.LogWarning("Blocking user {UserId} during checkout because their assigned company is deleted. RemovedCartItemCount: {CartItemCount}", applicationUser.Id, shoppingCarts.Count);
			_unitOfWork.ShoppingCart.RemoveRange(shoppingCarts);

			applicationUser.LockoutEnabled = true;
			applicationUser.LockoutEnd = DateTime.Now.AddYears(1000);
			applicationUser.SecurityStamp = Guid.NewGuid().ToString();

			_unitOfWork.Save();
			await _signInManager.SignOutAsync();
			HttpContext.Session.SetInt32(SD.SessionCart, 0);
			return RedirectToAction("Index", "Home");
		}

		private decimal GetPriceBasedOnRole(ShoppingCart shoppingCart)
		{
			if (User.IsInRole(SD.Role_Company))
			{
				return shoppingCart.Product.FinalWholesalePrice;
			}
			else
			{
				return shoppingCart.Product.FinalRetailPrice;
			}
		}

	}
}