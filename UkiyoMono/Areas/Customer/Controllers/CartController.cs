using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Localization;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages;
using Stripe;
using Stripe.Checkout;
using Stripe.Issuing;
using System.Globalization;
using System.Security.Claims;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models;
using UkiyoDesigns.Models.ViewModels;
using UkiyoDesigns.Utility;

namespace UkiyoDesignsWeb.Areas.Customer.Controllers
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
		public CartController(IUnitOfWork unitOfWork,IStringLocalizer<CartController> localizer, SignInManager<ApplicationUser> signInManager)
		{
			_localizer = localizer;
			_unitOfWork = unitOfWork;
			_signInManager = signInManager;
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
				ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId , includeProperties: "Product"),
				OrderHeader = new()
			};
			IEnumerable<ProductImage> productImages = _unitOfWork.ProductImage.GetAll();
			RemoveShoppingCartsOutdated(userId);
			foreach (var cart in ShoppingCartVM.ShoppingCartList)
			{
				cart.Product.ProductImages = productImages.Where(u => u.ProductId == cart.Product.Id).ToList();
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
			ShoppingCartVM.ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId, includeProperties: "Product");
		
		}

		public async Task<IActionResult> Summary()
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized();
			}
			ShoppingCartVM = new()
			{
				ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId, includeProperties: "Product"),
				OrderHeader = new()
			};
			var applicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId, tracked: true);
			if (applicationUser == null)
			{
				return Unauthorized();
			}
			if (HasDeletedCompany(applicationUser))
			{
				return await ClearCartAndBlockUser(applicationUser);
			}

			ShoppingCartVM.OrderHeader.ApplicationUser = applicationUser;
			ShoppingCartVM.OrderHeader.ApplicationUserId = userId;
			ShoppingCartVM.OrderHeader.PhoneNumber = applicationUser.PhoneNumber ?? string.Empty;
			ShoppingCartVM.OrderHeader.StreetAddress = applicationUser.StreetAddress ?? string.Empty;
			ShoppingCartVM.OrderHeader.City = applicationUser.City ?? string.Empty;
			ShoppingCartVM.OrderHeader.State = applicationUser.State ?? string.Empty;
			ShoppingCartVM.OrderHeader.PostalCode = applicationUser.PostalCode ?? string.Empty;
			ShoppingCartVM.OrderHeader.Name = ShoppingCartVM.OrderHeader.ApplicationUser.Name;

			foreach (var cart in ShoppingCartVM.ShoppingCartList)
			{
				cart.Price = GetPriceBasedOnRole(cart);
				ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
			}
			return View(ShoppingCartVM);
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
			ShoppingCartVM.ShoppingCartList = _unitOfWork.ShoppingCart
				.GetAll(u => u.ApplicationUserId == userId && u.Product.IsDeleted == false && u.Product.IsAvailableInStore == true, includeProperties: "Product");
			ShoppingCartVM.OrderHeader.ApplicationUserId = userId;
			ShoppingCartVM.OrderHeader.OrderDate = System.DateTime.Now;

			ApplicationUser? applicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId, tracked: true);
			if (applicationUser == null)
			{
				return Unauthorized();
			}
			if (HasDeletedCompany(applicationUser))
			{
				return await ClearCartAndBlockUser(applicationUser);
			}

			ShoppingCartVM.OrderHeader.OrderTotal = 0;
			foreach (var cart in ShoppingCartVM.ShoppingCartList)
			{
				cart.Price = GetPriceBasedOnRole(cart);
				ShoppingCartVM.OrderHeader.OrderTotal += cart.Price * cart.Count;
			}

			if (ShoppingCartVM.OrderHeader.OrderTotal <= 0)
			{
				ModelState.AddModelError("OrderHeader", _localizer["OrderTotalZeroError"].Value);
				return RedirectToAction("Index", "Home");
			}
			if (!ModelState.IsValid)
				return View(ShoppingCartVM);

			if (applicationUser.CompanyId.GetValueOrDefault() == 0)
			{
				//it is a regular costumer acount
				ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusPending;
				ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusPending;
			}
			else
			{
				//it is a company
				ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusDelayedPayment;
				ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusApproved;
			}
			_unitOfWork.OrderHeader.Add(ShoppingCartVM.OrderHeader);
			_unitOfWork.Save();
			foreach (var cart in ShoppingCartVM.ShoppingCartList)
			{
				OrderDetail orderDetail = new()
				{
					ProductId = cart.ProductId,
					OrderHeaderId = ShoppingCartVM.OrderHeader.Id,
					Price = cart.Price,
					Count = cart.Count
				};
				_unitOfWork.OrderDetail.Add(orderDetail);
				_unitOfWork.Save();
			}
			if (applicationUser.CompanyId.GetValueOrDefault() == 0)
			{
				//it is a regular costumer acount and we need to capture payment
				//stripe logic
				var culture = CultureInfo.CurrentCulture.Name;
				var domain = Request.Scheme + "://" + Request.Host.Value + "/" + culture + "/";
				var options = new SessionCreateOptions
				{
					SuccessUrl = domain + $"customer/cart/OrderConfirmation?id={ShoppingCartVM.OrderHeader.Id}",
					CancelUrl = domain + $"customer/cart/index",
					LineItems = new List<SessionLineItemOptions>(),
					Mode = "payment",
				};
				foreach (var item in ShoppingCartVM.ShoppingCartList)
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
				_unitOfWork.OrderHeader.UpdateStripePaymentId(ShoppingCartVM.OrderHeader.Id, session.Id, session.PaymentIntentId);
				_unitOfWork.Save();
				Response.Headers["Location"] = session.Url;
				return new StatusCodeResult(303);
			}
			return RedirectToAction(nameof(OrderConfirmation), new { id = ShoppingCartVM.OrderHeader.Id });
		}

		public IActionResult OrderConfirmation(int id)
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

			if (orderHeader.PaymentStatus != SD.PaymentStatusDelayedPayment)
			{
				// order made by customer
				var service = new SessionService();
				Session session = service.Get(orderHeader.SessionId);
				if (session.PaymentStatus.ToLower() == "paid")
				{
					_unitOfWork.OrderHeader.UpdateStripePaymentId(id, session.Id, session.PaymentIntentId);
					_unitOfWork.OrderHeader.UpdateStatus(id, SD.StatusApproved, SD.PaymentStatusApproved);
					_unitOfWork.Save();
				}
				HttpContext.Session.Clear();
			}
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (!string.IsNullOrEmpty(userId) && orderHeader.ApplicationUserId == userId)
			{
				List<ShoppingCart> shoppingCarts = _unitOfWork.ShoppingCart
					.GetAll(u => u.ApplicationUserId == orderHeader.ApplicationUserId).ToList();
				_unitOfWork.ShoppingCart.RemoveRange(shoppingCarts);
				_unitOfWork.Save();
			}
			return View(id);
		}

		private bool UserCanAccessOrder(OrderHeader orderHeader)
		{
			if (User.IsInRole(SD.Role_Admin) || User.IsInRole(SD.Role_Employee))
			{
				return true;
			}

			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			return !string.IsNullOrEmpty(userId) && orderHeader.ApplicationUserId == userId;
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

		private async Task<IActionResult> ClearCartAndBlockUser(ApplicationUser applicationUser)
		{
			var shoppingCarts = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == applicationUser.Id).ToList();
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
