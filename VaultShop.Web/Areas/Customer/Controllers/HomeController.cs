using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Models.ViewModels;
using VaultShop.Utility;





namespace VaultShop.Web.Areas.Customer.Controllers
{
	[Area("Customer")]
	public class HomeController : Controller
	{
		private readonly ILogger<HomeController> _logger;
		private readonly IUnitOfWork _unitOfWork;
		private readonly IStringLocalizer<HomeController> _localizer;

		public HomeController(ILogger<HomeController> logger, IUnitOfWork unitOfWork, IStringLocalizer<HomeController> localizer)
		{
			_localizer = localizer;
			_logger = logger;
			_unitOfWork = unitOfWork;
		}

		public IActionResult Index()
		{
			var productList = _unitOfWork.Product
				.GetAll(u => u.IsDeleted == false && u.IsAvailableInStore == true, includeProperties: "Category,ProductImages")
				.ToList();
			var featuredProducts = productList
				.Where(u => u.IsFeatured)
				.OrderBy(u => u.FeaturedSortOrder)
				.ThenBy(u => u.Id)
				.ToList();
			var culture = CultureInfo.CurrentCulture.Name;

			return View(new HomeIndexVM
			{
				Products = productList,
				FeaturedProducts = featuredProducts
			});
		}


		public IActionResult Details(int productId)
		{
			var product = _unitOfWork.Product.Get(u => u.IsDeleted == false && u.IsAvailableInStore == true && u.Id == productId, includeProperties: "Category,ProductImages");
			if (product == null)
			{
				return NotFound();
			}

			ShoppingCart cart = new()
			{
				Product = product,
				Count = 1,
				ProductId = productId
			};

			if (User.Identity?.IsAuthenticated == true)
			{
				var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
				if (string.IsNullOrEmpty(userId))
				{
					return Unauthorized();
				}

				cart.ApplicationUserId = userId;
				FavoriteProduct? isFavorite = _unitOfWork.FavoriteProduct.Get(u => u.ProductId == cart.ProductId && userId == u.ApplicationUserId);
				if (isFavorite != null)
				{
					cart.IsFavorite = true;
					cart.FavoriteProductId = isFavorite.Id;
				}
			}

			return View(cart);
		}

		[HttpPost]
		[Authorize]
		public IActionResult Details(ShoppingCart shoppingCart)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized();
			}

			shoppingCart.ApplicationUserId = userId;

			if (!ModelState.IsValid)
				return RedirectToAction(nameof(Index));

			var product = _unitOfWork.Product.Get(u => u.Id == shoppingCart.ProductId && u.IsDeleted == false && u.IsAvailableInStore == true);
			if (product == null)
			{
				TempData["error"] = _localizer["ProductUnavailable"].Value;
				return RedirectToAction(nameof(Index));
			}

			ShoppingCart? cartFromDb = _unitOfWork.ShoppingCart
				.Get(u => u.ApplicationUserId == userId && u.ProductId == shoppingCart.ProductId && u.Product.IsDeleted == false && u.Product.IsAvailableInStore == true);
			if (cartFromDb != null)
			{
				cartFromDb.Count += shoppingCart.Count;
				_unitOfWork.ShoppingCart.Update(cartFromDb);
				_unitOfWork.Save();
				TempData["success"] = _localizer["CartUpdatedSuccess"].Value;
			}
			else
			{
				_unitOfWork.ShoppingCart.Add(shoppingCart);
				_unitOfWork.Save();
				HttpContext.Session.SetInt32(SD.SessionCart,
					_unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId).Count());
				TempData["success"] = _localizer["ProductAddCart"].Value;
			}
			return RedirectToAction(nameof(Details), new { productId = shoppingCart.ProductId });
		}


		public IActionResult Privacy()
		{
			return View();
		}

		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		public IActionResult Error()
		{
			return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
		}


		public IActionResult Search(string searchString)
		{
			if (!string.IsNullOrEmpty(searchString))
			{
				IEnumerable<Product> searchProductList = _unitOfWork.Product
					.GetAll(u => u.IsDeleted == false && u.IsAvailableInStore == true &&
					(u.Name.Contains(searchString) || u.Category.Name.Contains(searchString)
					|| u.Description.Contains(searchString)), includeProperties: "Category,ProductImages");
				if (!searchProductList.Any())
				{
					TempData["error"] = _localizer["SearchNoMatches"].Value;
					return RedirectToAction("Index");
				}
				return View(searchProductList);
			}
			else
			{
				TempData["error"] = _localizer["SearchEmpty"].Value;
				return RedirectToAction("Index");
			}
		}

		public IActionResult SetLanguage(string culture, string returnUrl)
		{
			if ((returnUrl.Contains("es-AR") || returnUrl.Contains("en-US"))&& !string.IsNullOrEmpty(returnUrl))
			{
				if (returnUrl.Contains("es-AR"))
				{
					returnUrl=returnUrl.Replace("es-AR", culture);
				}
				else
				{
					returnUrl=returnUrl.Replace("en-US", culture);
				}
			}
			if (returnUrl == "/")
			{
				returnUrl += culture;
			}
			Response.Cookies.Append(
				CookieRequestCultureProvider.DefaultCookieName,
				CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
				new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
			return LocalRedirect(returnUrl);
		}

		public IActionResult FAQs()
		{
			return View();
		}
		public IActionResult TakeCare()
		{
			return View();
		}
		public IActionResult AboutUs()
		{
			return View();
		}
		[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee+ "," + SD.Role_Company)]
		public IActionResult Wholesale()
		{
			return View();
		}
		[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
		public IActionResult Help()
		{
			return View();
		}
		[HttpGet]
		public IActionResult GetTranslations()
		{
			var translations = new
			{
				Yes = _localizer["Yes"].Value,
				No= _localizer["No"].Value,
				AreYouSure =_localizer["AreYouSure"].Value,
				YouWontRevert = _localizer["YouWontRevert"].Value,
				DeleteConfirmation = _localizer["DeleteConfirmation"].Value,
				AvailablesInStore = _localizer["AvailablesInStore"].Value,
				ExportToPDF = _localizer["ExportToPDF"].Value,
				ColumnsVisibility = _localizer["ColumnsVisibility"].Value,
				Columns = _localizer["Columns"].Value,
				Copy = _localizer["Copy"].Value,
			};

			return Json(translations);
		}

	}
}
