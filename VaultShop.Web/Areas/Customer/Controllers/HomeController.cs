using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Models.Pagination;
using VaultShop.Models.ViewModels;
using VaultShop.Utility;
using VaultShop.Web.Services.Pagination;





namespace VaultShop.Web.Areas.Customer.Controllers
{
	[Area("Customer")]
	public class HomeController : Controller
	{
		private readonly ILogger<HomeController> _logger;
		private readonly IUnitOfWork _unitOfWork;
		private readonly IStringLocalizer<HomeController> _localizer;
		private readonly PaginationOptions _paginationOptions;

		public HomeController(ILogger<HomeController> logger, IUnitOfWork unitOfWork, IStringLocalizer<HomeController> localizer, IOptions<PaginationOptions> paginationOptions)
		{
			_localizer = localizer;
			_logger = logger;
			_unitOfWork = unitOfWork;
			_paginationOptions = paginationOptions.Value;
		}

		public IActionResult Index(int pageNumber = 1)
		{
			var productList = _unitOfWork.Product
				.GetAll(u => u.IsDeleted == false && u.IsAvailableInStore == true, includeProperties: "Category,ProductImages")
				.OrderBy(u => u.Id)
				.ToList();
			var featuredProducts = productList
				.Where(u => u.IsFeatured)
				.OrderBy(u => u.FeaturedSortOrder)
				.ThenBy(u => u.Id)
				.ToList();
			var categories = productList
				.Where(p => p.Category != null && !string.IsNullOrWhiteSpace(p.Category.Name))
				.GroupBy(p => p.Category.Id)
				.Select(g => g.First().Category)
				.OrderBy(c => c.Name)
				.ToList();

			var pagedProducts = PagedList<Product>.Create(productList, pageNumber, _paginationOptions.PageSize);
			if (productList.Count > 0 && pageNumber > pagedProducts.TotalPages)
			{
				return RedirectToAction(nameof(Index), new { pageNumber = pagedProducts.TotalPages });
			}

			return View(new HomeIndexVM
			{
				Products = pagedProducts,
				FeaturedProducts = featuredProducts,
				Categories = categories
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

			if (shoppingCart.Count < 1)
			{
				TempData["error"] = _localizer["NotEnoughStock"].Value;
				return RedirectToAction(nameof(Details), new { productId = shoppingCart.ProductId });
			}

			var product = _unitOfWork.Product.Get(u => u.Id == shoppingCart.ProductId && u.IsDeleted == false && u.IsAvailableInStore == true);
			if (product == null)
			{
				TempData["error"] = _localizer["ProductUnavailable"].Value;
				return RedirectToAction(nameof(Index));
			}

			// ponytail: guard at write edges only, not cart read (design.md decision 2)
			var existingCartCount = _unitOfWork.ShoppingCart
				.Get(u => u.ApplicationUserId == userId && u.ProductId == shoppingCart.ProductId)?.Count ?? 0;
			if (existingCartCount + shoppingCart.Count > product.StockQuantity)
			{
				TempData["error"] = _localizer["NotEnoughStock"].Value;
				return RedirectToAction(nameof(Details), new { productId = shoppingCart.ProductId });
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

		public IActionResult Terms()
		{
			return View();
		}

		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		[Route("/Home/Error")]
		public IActionResult Error(int? statusCode)
		{
			var code = statusCode ?? Response.StatusCode;
			var title = _localizer[code == 404 ? "ErrorNotFound" : "ErrorTitle"].Value;
			var message = _localizer[code == 404 ? "ErrorMessageNotFound" : "ErrorMessage"].Value;

			return View(new ErrorViewModel
			{
				StatusCode = code,
				Title = title,
				Message = message,
				RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
			});
		}


		public IActionResult Search(string searchString, int pageNumber = 1)
		{
			if (string.IsNullOrWhiteSpace(searchString))
			{
				TempData["error"] = _localizer["SearchEmpty"].Value;
				return RedirectToAction("Index");
			}

			var products = _unitOfWork.Product
				.GetAll(u => u.IsDeleted == false && u.IsAvailableInStore == true, includeProperties: "Category,ProductImages")
				.OrderBy(u => u.Id)
				.ToList();

			var compareInfo = CultureInfo.CurrentCulture.CompareInfo;
			var compareOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

			var searchProductList = products
				.Where(p => compareInfo.IndexOf(p.Name, searchString, compareOptions) >= 0
						 || compareInfo.IndexOf(p.Category?.Name ?? string.Empty, searchString, compareOptions) >= 0
						 || compareInfo.IndexOf(p.Description ?? string.Empty, searchString, compareOptions) >= 0)
				.OrderBy(p => p.Id)
				.ToList();

			if (searchProductList.Count == 0)
			{
				TempData["error"] = _localizer["SearchNoMatches"].Value;
				return RedirectToAction("Index");
			}

			var pagedProducts = PagedList<Product>.Create(searchProductList, pageNumber, _paginationOptions.PageSize);
			if (pageNumber > pagedProducts.TotalPages)
			{
				return RedirectToAction(nameof(Search), new { searchString, pageNumber = pagedProducts.TotalPages });
			}

			return View(pagedProducts);
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
				new CookieOptions { Path = "/", Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });
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
