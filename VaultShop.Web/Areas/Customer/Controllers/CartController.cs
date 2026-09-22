using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using System.Globalization;
using System.Security.Claims;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Models.ViewModels;
using VaultShop.Utility;
using VaultShop.Web.Services;
using VaultShop.Web.Services.Checkout;
using VaultShop.Web.Services.Email;
using VaultShop.Web.Services.Payments;
using VaultShop.Web.Services.Pricing;
using VaultShop.Web.Services.Shipping;

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
		private readonly IServiceProvider _paymentSessionServiceProvider;
		private readonly IPaymentStatusService _paymentStatusService;
		private readonly ITransactionalEmailService _emailService;
		private readonly IConfiguration _configuration;
		private readonly OrderAccessPolicy _orderAccessPolicy;
		private readonly IBranchLookupService _branchLookup;
		// ponytail: must match BranchCascadePickerVM.IdPrefix — failed POSTs reselect province/locality from these form fields.
		private const string BranchPickerPrefix = "branchPicker";
		public CartController(IUnitOfWork unitOfWork,IStringLocalizer<CartController> localizer, SignInManager<ApplicationUser> signInManager,
			ILogger<CartController> logger, ICheckoutService checkoutService, IServiceProvider paymentSessionServiceProvider,
			IPaymentStatusService paymentStatusService, ITransactionalEmailService emailService, IConfiguration configuration, OrderAccessPolicy orderAccessPolicy,
			IBranchLookupService branchLookup)
		{
			_localizer = localizer;
			_unitOfWork = unitOfWork;
			_signInManager = signInManager;
			_logger = logger;
			_checkoutService = checkoutService;
			_paymentSessionServiceProvider = paymentSessionServiceProvider;
			_paymentStatusService = paymentStatusService;
			_emailService = emailService;
			_configuration = configuration;
			_orderAccessPolicy = orderAccessPolicy;
			_branchLookup = branchLookup;
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
			var useWholesale = PricingHelper.ShouldUseWholesale(User, HttpContext);
			foreach (var cart in ShoppingCartVM.ShoppingCartList)
			{
				cart.Price = useWholesale ? cart.Product.FinalWholesalePrice : cart.Product.FinalRetailPrice;
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

			var result = _checkoutService.BuildSummary(userId, PricingHelper.ShouldUseWholesale(User, HttpContext));

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
		PopulatePaymentMethodViewData();
		ViewData["BranchCascadePicker"] = BuildBranchCascadePickerVM();
		return View(result.ShoppingCartVM);
	}

	// ponytail: deterministic cascade Provincia → Localidad → Sucursal; branch rows are public data, cacheable, and shared by checkout + admin correction.
	[HttpGet]
	[AllowAnonymous]
	[ResponseCache(Duration = 300)]
	public IActionResult BranchProvinces()
	{
		return Json(_branchLookup.GetCandidateProvinces()
			.Select(p => new { code = p.Code, name = p.Name }));
	}

	[HttpGet]
	[AllowAnonymous]
	[ResponseCache(Duration = 60)]
	public IActionResult BranchLocalities(string provinceCode)
	{
		if (string.IsNullOrWhiteSpace(provinceCode))
		{
			return BadRequest();
		}

		return Json(_branchLookup.GetCandidateLocalities(provinceCode));
	}

	[HttpGet]
	[AllowAnonymous]
	[ResponseCache(Duration = 60)]
	public IActionResult BranchBranches(string provinceCode, string locality)
	{
		if (string.IsNullOrWhiteSpace(provinceCode) || string.IsNullOrWhiteSpace(locality))
		{
			return BadRequest();
		}

		return Json(_branchLookup.GetCandidateBranches(provinceCode, locality)
			.Select(a => new
			{
				code = a.Code,
				name = a.Name,
				address = (a.Number.HasValue ? $"{a.Street} {a.Number}" : a.Street).Trim(),
				locality = a.Locality,
				province = a.Province,
				hours = string.IsNullOrWhiteSpace(a.Hours) ? PostalAgency.HoursUnknown : a.Hours
			}));
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
			var summaryResult = _checkoutService.BuildSummary(userId, PricingHelper.ShouldUseWholesale(User, HttpContext));
			PopulatePaymentMethodViewData();
			return View(RestorePostedHeaderAndCandidates(summaryResult.ShoppingCartVM));
		}

		// ponytail: snapshot is re-resolved from PostalAgency by code — client name/address are never trusted.
		var agency = _branchLookup.GetByCode(ShoppingCartVM.OrderHeader.PickupAgencyCode);
		if (agency is null)
		{
			ModelState.AddModelError("OrderHeader.PickupAgencyCode", _localizer["PickupAgencyRequired"].Value);
			var summaryResult = _checkoutService.BuildSummary(userId, PricingHelper.ShouldUseWholesale(User, HttpContext));
			if (summaryResult.IsCartEmpty)
			{
				TempData["error"] = _localizer["CartEmptyOrInvalidError"].Value;
				return RedirectToAction(nameof(Index));
			}
			PopulatePaymentMethodViewData();
			return View(RestorePostedHeaderAndCandidates(summaryResult.ShoppingCartVM));
		}

		ShoppingCartVM.OrderHeader.PickupAgencyHours =
		string.IsNullOrWhiteSpace(agency.Hours) ? PostalAgency.HoursUnknown : agency.Hours;

	ShoppingCartVM.OrderHeader.DeliveryType = SD.DeliveryTypePickup;
		ShoppingCartVM.OrderHeader.PickupAgencyCode = agency.Code;
		ShoppingCartVM.OrderHeader.PickupAgencyName = agency.Name;
		ShoppingCartVM.OrderHeader.PickupAgencyAddress =
			(agency.Number.HasValue ? $"{agency.Street} {agency.Number}" : agency.Street).Trim()
			+ $", {agency.Locality}, {agency.Province} {agency.PostalCode}";

		var isCompanyCheckout = User.IsInRole(SD.Role_Company);
			var useWholesalePrice = PricingHelper.ShouldUseWholesale(User, HttpContext);
			if (isCompanyCheckout)
			{
				ShoppingCartVM.OrderHeader.PaymentMethod = null;
			}
			else
			{
				var allowedPaymentMethods = new List<string>();
				if (_configuration.GetValue("Payments:StripeEnabled", true)) allowedPaymentMethods.Add(SD.PaymentMethodStripe);
				if (_configuration.GetValue("Payments:BankTransferEnabled", true)) allowedPaymentMethods.Add(SD.PaymentMethodBankTransfer);
				if (_configuration.GetValue("Payments:MercadoPagoEnabled", false)) allowedPaymentMethods.Add(SD.PaymentMethodMercadoPago);

				if (ShoppingCartVM.OrderHeader.PaymentMethod is not string paymentMethod || !allowedPaymentMethods.Contains(paymentMethod))
				{
					TempData["error"] = _localizer["InvalidPaymentMethodError"].Value;
					return RedirectToAction(nameof(Index));
				}
			}

			var result = _checkoutService.CreateOrder(userId, ShoppingCartVM.OrderHeader, useWholesalePrice);

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
			if (result.InsufficientStock)
			{
				TempData["error"] = _localizer["NotEnoughStock"].Value;
				return RedirectToAction(nameof(Index));
			}

			if (result.OrderId is null || result.ShoppingCartVM is null)
			{
				throw new InvalidOperationException("Checkout order creation succeeded without returning order details.");
			}

			var orderId = result.OrderId.Value;

			// ponytail: wholesale admin already notified at creation; paid transition skips duplicate
			if (isCompanyCheckout)
			{
				await _emailService.TrySendOrderConfirmationAsync(orderId);
				await _emailService.TrySendAdminNewOrderAlertAsync(orderId);
			}
			else if (result.ShoppingCartVM.OrderHeader.PaymentMethod == SD.PaymentMethodBankTransfer)
			{
				await _emailService.TrySendOrderConfirmationAsync(orderId);
			}

			if (result.RequiresOnlinePayment)
			{
				var culture = CultureInfo.CurrentCulture.Name;
				var configuredSiteUrl = _configuration["SiteUrl"];
				var publicBaseUrl = string.IsNullOrWhiteSpace(configuredSiteUrl)
					? Request.Scheme + "://" + Request.Host.Value
					: configuredSiteUrl.TrimEnd('/');
				var domain = publicBaseUrl + "/" + culture + "/";
				var successUrl = result.ShoppingCartVM.OrderHeader.PaymentMethod == SD.PaymentMethodMercadoPago
					? domain + $"customer/cart/OrderConfirmation?id={orderId}"
					: domain + $"customer/cart/OrderConfirmation?id={orderId}&session_id={{CHECKOUT_SESSION_ID}}";
				PaymentSessionResult session;
				try
				{
					session = GetPaymentSessionService(result.ShoppingCartVM.OrderHeader).CreateCheckoutSession(new PaymentSessionRequest(
						orderId,
						result.ShoppingCartVM.ShoppingCartList.Select(item => new PaymentSessionLineItem(item.Product.Name, item.Price, item.Count)),
						successUrl,
						domain + "customer/cart/index",
						result.ShoppingCartVM.OrderHeader.PaymentMethod == SD.PaymentMethodMercadoPago
							? publicBaseUrl + "/api/mercadopago/webhook?source_news=webhooks"
							: null));
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

		public async Task<IActionResult> OrderConfirmation(int id, [FromQuery(Name = "session_id")] string? sessionId, [FromQuery(Name = "preference_id")] string? preferenceId, [FromQuery(Name = "payment_id")] string? paymentId)
		{
			OrderHeader? orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == id, includeProperties: "ApplicationUser");
			if (orderHeader == null)
			{
				return NotFound();
			}
			if (!_orderAccessPolicy.CanAccess(orderHeader, User))
			{
				return NotFound();
			}
			var confirmationSessionId = sessionId ?? (orderHeader.PaymentMethod == SD.PaymentMethodMercadoPago ? preferenceId : null);
			if (!ConfirmationSessionMatches(orderHeader, confirmationSessionId))
			{
				_logger.LogWarning("Rejected order confirmation for order {OrderId} with session {SessionId}. Stored session: {StoredSessionId}.", orderHeader.Id, confirmationSessionId, orderHeader.SessionId);
				return NotFound();
			}

			if (orderHeader.OrderStatus == SD.StatusPending && orderHeader.PaymentStatus == SD.PaymentStatusPending)
			{
				await SyncPaidCheckoutSession(orderHeader, paymentId);
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
			PopulateBankTransferViewData();
			return View(orderHeader);
		}

		private void PopulatePaymentMethodViewData()
		{
			ViewData["StripeEnabled"] = _configuration.GetValue("Payments:StripeEnabled", true);
			ViewData["MercadoPagoEnabled"] = _configuration.GetValue("Payments:MercadoPagoEnabled", false);
			ViewData["ShowDemoNotice"] = _configuration.GetValue("Storefront:ShowDemoNotice", true);
			PopulateBankTransferViewData();
		}

	// ponytail: cascade rehydration — posted province/locality reseed the picker's options server-side so a failed
	// POST keeps selection + candidates without geocoding. The picked code survives via the posted OrderHeader.
	private BranchCascadePickerVM BuildBranchCascadePickerVM(string? provinceCode = null, string? locality = null, string? branchCode = null)
	{
		var provinces = _branchLookup.GetCandidateProvinces();
		IReadOnlyList<string> localities = string.IsNullOrWhiteSpace(provinceCode)
			? []
			: _branchLookup.GetCandidateLocalities(provinceCode);
		IReadOnlyList<BranchOption> branches = (string.IsNullOrWhiteSpace(provinceCode) || string.IsNullOrWhiteSpace(locality))
			? []
			: _branchLookup.GetCandidateBranches(provinceCode, locality).Select(ToBranchOption).ToList();
		return new BranchCascadePickerVM
		{
			FieldName = "OrderHeader.PickupAgencyCode",
			IdPrefix = BranchPickerPrefix,
			SubmitButtonId = "placeOrderBtn",
			ProvincesUrl = Url.Action("BranchProvinces", "Cart", new { area = "Customer" }) ?? string.Empty,
			LocalitiesUrl = Url.Action("BranchLocalities", "Cart", new { area = "Customer" }) ?? string.Empty,
			BranchesUrl = Url.Action("BranchBranches", "Cart", new { area = "Customer" }) ?? string.Empty,
			Provinces = provinces,
			SelectedProvinceCode = provinceCode ?? string.Empty,
			Localities = localities,
			SelectedLocality = locality ?? string.Empty,
			Branches = branches,
			SelectedBranchCode = branchCode ?? string.Empty,
		};
	}

	private static BranchOption ToBranchOption(PostalAgency agency)
	{
		return new BranchOption(
			agency.Code,
			agency.Name,
			(agency.Number.HasValue ? $"{agency.Street} {agency.Number}" : agency.Street).Trim(),
			agency.Locality,
			agency.Province,
			string.IsNullOrWhiteSpace(agency.Hours) ? PostalAgency.HoursUnknown : agency.Hours);
	}

	// ponytail: failure-path only — posted scalars overlay the authoritative rebuilt VM (cart/totals stay server-built),
	// then the cascade picker rehydrates selection + candidates from the posted province/locality. Never throws.
	private ShoppingCartVM RestorePostedHeaderAndCandidates(ShoppingCartVM? fresh)
	{
		var posted = ShoppingCartVM.OrderHeader;
		var vm = fresh ?? ShoppingCartVM;
		if (!ReferenceEquals(vm, ShoppingCartVM) && vm.OrderHeader is not null && posted is not null)
		{
			vm.OrderHeader.Name = posted.Name ?? string.Empty;
			vm.OrderHeader.PhoneNumber = posted.PhoneNumber ?? string.Empty;
			vm.OrderHeader.StreetAddress = posted.StreetAddress ?? string.Empty;
			vm.OrderHeader.City = posted.City ?? string.Empty;
			vm.OrderHeader.State = posted.State ?? string.Empty;
			vm.OrderHeader.PostalCode = posted.PostalCode ?? string.Empty;
			vm.OrderHeader.PaymentMethod = posted.PaymentMethod;
			vm.OrderHeader.PickupAgencyCode = posted.PickupAgencyCode;
		}

		string? province = null;
		string? locality = null;
		try
		{
			var form = HttpContext.Request.Form;
			province = form[$"{BranchPickerPrefix}Province"].ToString();
			locality = form[$"{BranchPickerPrefix}Locality"].ToString();
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Branch cascade selection rehydration failed during checkout.");
		}
		ViewData["BranchCascadePicker"] = BuildBranchCascadePickerVM(province, locality, vm.OrderHeader?.PickupAgencyCode);
		return vm;
	}

		private void PopulateBankTransferViewData()
		{
			ViewData["BankTransferEnabled"] = _configuration.GetValue("Payments:BankTransferEnabled", true);
			ViewData["BankTransferCbu"] = _configuration.GetValue<string>("Payments:BankTransferCbu") ?? string.Empty;
			ViewData["BankTransferAlias"] = _configuration.GetValue<string>("Payments:BankTransferAlias") ?? string.Empty;
			ViewData["BankTransferRecipientName"] = _configuration.GetValue<string>("Payments:BankTransferRecipientName") ?? string.Empty;
			ViewData["BankTransferBankName"] = _configuration.GetValue<string>("Payments:BankTransferBankName") ?? string.Empty;
		}

		private static bool ConfirmationSessionMatches(OrderHeader orderHeader, string? sessionId)
		{
			return string.IsNullOrWhiteSpace(orderHeader.SessionId) ||
				string.Equals(orderHeader.SessionId, sessionId, StringComparison.Ordinal);
		}

		private async Task SyncPaidCheckoutSession(OrderHeader orderHeader, string? paymentId)
		{
			if (string.IsNullOrWhiteSpace(orderHeader.SessionId))
			{
				return;
			}

			try
			{
				var session = GetPaymentSessionService(orderHeader).GetCheckoutSessionStatus(orderHeader.SessionId, paymentId);
				if (orderHeader.PaymentMethod == SD.PaymentMethodMercadoPago && !string.IsNullOrWhiteSpace(paymentId) &&
					(!int.TryParse(session.ExternalReference, NumberStyles.None, CultureInfo.InvariantCulture, out var externalOrderId) ||
						externalOrderId != orderHeader.Id || session.TransactionAmount != orderHeader.OrderTotal))
				{
					_logger.LogWarning("Ignored Mercado Pago payment {PaymentId} for order {OrderId} because provider reference or amount did not match. ExternalReference: {ExternalReference}. TransactionAmount: {TransactionAmount}. OrderTotal: {OrderTotal}.", paymentId, orderHeader.Id, session.ExternalReference, session.TransactionAmount, orderHeader.OrderTotal);
					return;
				}
				if (session.IsPaid)
				{
					await _paymentStatusService.MarkCheckoutSessionPaid(new PaymentSessionStatusUpdate(orderHeader.Id, session.SessionId, session.PaymentIntentId));
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Could not sync payment status for order {OrderId} from checkout session {SessionId}.", orderHeader.Id, orderHeader.SessionId);
			}
		}

		private IPaymentSessionService GetPaymentSessionService(OrderHeader orderHeader)
		{
			var paymentMethod = orderHeader.PaymentMethod ?? SD.PaymentMethodStripe;
			if (paymentMethod is not (SD.PaymentMethodStripe or SD.PaymentMethodMercadoPago))
			{
				throw new InvalidOperationException($"Order {orderHeader.Id} does not use an online payment provider.");
			}

			return _paymentSessionServiceProvider.GetRequiredKeyedService<IPaymentSessionService>(paymentMethod);
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

		// ponytail: guard at write edges only, not cart read (design.md decision 2)
		var product = _unitOfWork.Product.Get(u => u.Id == cartFromDb.ProductId && !u.IsDeleted && u.IsAvailableInStore);
		if (product == null || cartFromDb.Count + 1 > product.StockQuantity)
		{
			TempData["error"] = _localizer["NotEnoughStock"].Value;
			return RedirectToAction(nameof(Index));
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

	}
}
