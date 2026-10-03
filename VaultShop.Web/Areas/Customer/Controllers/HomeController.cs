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
				.GetAll(u => u.IsDeleted == false && u.IsAvailableInStore == true, includeProperties: "Category,ProductImages,Keywords.Keyword.Images")
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
				Categories = categories,
				Collections = HomeIndexVM.ComputeCollections(productList)
			});
		}


		public IActionResult Details(int productId, string? slug = null)
		{
			var product = _unitOfWork.Product.Get(u => u.IsDeleted == false && u.IsAvailableInStore == true && u.Id == productId, includeProperties: "Category,ProductImages,Keywords.Keyword.Images");
			if (product == null)
			{
				return NotFound();
			}

			// id is the lookup truth, slug is decorative: 301 only when a wrong non-empty slug is given.
			// Absent slug renders as-is (bookmarks); null canonical (never upserted) has nothing to redirect to.
			if (!string.IsNullOrWhiteSpace(slug) && !string.IsNullOrWhiteSpace(product.Slug)
				&& !string.Equals(slug, product.Slug, StringComparison.Ordinal))
			{
				return RedirectToRoutePermanent("productDetails", new { productId, slug = product.Slug });
			}

			// product-slugs 3.1: canonical tag always advertises the slug form (matches the sitemap),
			// even when the shopper arrived on the id-anchor URL.
			ViewData["CanonicalUrl"] = Url.RouteUrl("productDetails", new { productId, slug = product.Slug });

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

			var detailCollections = product.Keywords
				.Where(pk => pk.Keyword != null && !pk.Keyword.IsDeleted)
				.Select(pk => new CollectionChipVM
				{
					Id = pk.KeywordId,
					Name = pk.Keyword.Name,
					Slug = pk.Keyword.Slug ?? string.Empty,
					ChipImageUrl = pk.Keyword.Images.FirstOrDefault(i => i.Kind == KeywordImageKind.Chip)?.ImageUrl,
					CoverImageUrl = pk.Keyword.Images.FirstOrDefault(i => i.Kind == KeywordImageKind.Cover)?.ImageUrl,
					MediumCoverImageUrl = pk.Keyword.Images.FirstOrDefault(i => i.Kind == KeywordImageKind.CoverMedium)?.ImageUrl,
					SmallCoverImageUrl = pk.Keyword.Images.FirstOrDefault(i => i.Kind == KeywordImageKind.CoverSmall)?.ImageUrl
				})
				.OrderBy(c => c.Name)
				.ToList();
			ViewData["DetailCollections"] = detailCollections;

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
				return RedirectToAction(nameof(Details), new { productId = shoppingCart.ProductId, slug = product.Slug });
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
			return RedirectToAction(nameof(Details), new { productId = shoppingCart.ProductId, slug = product.Slug });
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


		public IActionResult Search(string searchString, int? categoryId, int? keywordId, string? slug, string? cslug, int pageNumber = 1)
		{
			// 1.1 slug canonical redirect
			if (keywordId.HasValue)
			{
				var kw = _unitOfWork.Keyword.Get(k => k.Id == keywordId.Value && !k.IsDeleted);
				var canonical = kw?.Slug;
				ViewData["ActiveCollectionSlug"] = canonical;
				if (!string.IsNullOrWhiteSpace(slug) && !string.IsNullOrWhiteSpace(canonical)
					&& !string.Equals(slug, canonical, StringComparison.Ordinal))
				{
					return RedirectToActionPermanent(nameof(Search), new { searchString, categoryId, keywordId, slug = canonical, cslug, pageNumber });
				}
				// expose canonical even when slug missing so pager/chips can add it
				if (!string.IsNullOrWhiteSpace(canonical))
					ViewData["Slug"] = canonical;
			}

			// product-slugs 2.1: cslug mirrors the collection slug pattern for the category filter.
			if (categoryId.HasValue)
			{
				var canonicalCategory = _unitOfWork.Category.Get(c => c.Id == categoryId.Value && !c.IsDeleted)?.Slug;
				if (!string.IsNullOrWhiteSpace(cslug) && !string.IsNullOrWhiteSpace(canonicalCategory)
					&& !string.Equals(cslug, canonicalCategory, StringComparison.Ordinal))
				{
					return RedirectToActionPermanent(nameof(Search), new { searchString, categoryId, keywordId, slug, cslug = canonicalCategory, pageNumber });
				}
				if (!string.IsNullOrWhiteSpace(canonicalCategory))
					ViewData["CategorySlug"] = canonicalCategory;
			}
			// product-slugs 3.1: category "detail" is this filtered search, so that is the canonical.
			if (categoryId.HasValue && ViewData["CategorySlug"] is string categoryCanonical)
			{
				ViewData["CanonicalUrl"] = Url.Action(nameof(Search), new { categoryId, cslug = categoryCanonical });
			}

			if (string.IsNullOrWhiteSpace(searchString) && categoryId == null && keywordId == null)
			{
				TempData["error"] = _localizer["SearchEmpty"].Value;
				return RedirectToAction("Index");
			}

			var products = _unitOfWork.Product
				.GetAll(u => u.IsDeleted == false && u.IsAvailableInStore == true, includeProperties: "Category,ProductImages,Keywords.Keyword.Images")
				.OrderBy(u => u.Id)
				.ToList();

			// ponytail: missing/soft-deleted ids filter down to an empty set here, no extra lookup needed.
			IEnumerable<Product> filtered = products;

			if (categoryId.HasValue)
			{
				filtered = filtered.Where(p => p.CategoryId == categoryId.Value);
			}

			if (keywordId.HasValue)
			{
				filtered = filtered.Where(p => p.Keywords.Any(k => k.KeywordId == keywordId.Value));
			}

			if (!string.IsNullOrWhiteSpace(searchString))
			{
				var compareInfo = CultureInfo.CurrentCulture.CompareInfo;
				var compareOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

				filtered = filtered
					.Where(p => compareInfo.IndexOf(p.Name, searchString, compareOptions) >= 0
							 || compareInfo.IndexOf(p.Category?.Name ?? string.Empty, searchString, compareOptions) >= 0
							 || compareInfo.IndexOf(p.Description ?? string.Empty, searchString, compareOptions) >= 0);
			}

			var searchProductList = filtered.OrderBy(p => p.Id).ToList();

			if (searchProductList.Count == 0 && categoryId == null && keywordId == null)
			{
				TempData["error"] = _localizer["SearchNoMatches"].Value;
				return RedirectToAction("Index");
			}

			var pagedProducts = PagedList<Product>.Create(searchProductList, pageNumber, _paginationOptions.PageSize);
			if (searchProductList.Count > 0 && pageNumber > pagedProducts.TotalPages)
			{
				return RedirectToAction(nameof(Search), new { searchString, categoryId, keywordId, slug = ViewData["Slug"] as string ?? slug, cslug = ViewData["CategorySlug"] as string ?? cslug, pageNumber = pagedProducts.TotalPages });
			}

			var collections = HomeIndexVM.ComputeCollections(products);
			ViewData["Collections"] = collections;
			if (keywordId.HasValue)
			{
				var activeColl = collections.FirstOrDefault(c => c.Id == keywordId.Value);
				if (activeColl != null)
				{
					if (!string.IsNullOrWhiteSpace(activeColl.CoverImageUrl))
						ViewData["ActiveCollectionCover"] = activeColl.CoverImageUrl;
					if (!string.IsNullOrWhiteSpace(activeColl.MediumCoverImageUrl))
						ViewData["ActiveCollectionMediumCover"] = activeColl.MediumCoverImageUrl;
					if (!string.IsNullOrWhiteSpace(activeColl.SmallCoverImageUrl))
						ViewData["ActiveCollectionSmallCover"] = activeColl.SmallCoverImageUrl;
					ViewData["ActiveCollectionName"] = activeColl.Name;
					ViewData["ActiveCollectionCount"] = activeColl.Count;
					if (ViewData["Slug"] == null && !string.IsNullOrWhiteSpace(activeColl.Slug))
						ViewData["Slug"] = activeColl.Slug;
					if (ViewData["ActiveCollectionSlug"] == null)
						ViewData["ActiveCollectionSlug"] = activeColl.Slug;
				}
			}
			var allCategories = products
				.Where(p => p.Category != null && !string.IsNullOrWhiteSpace(p.Category.Name))
				.GroupBy(p => p.Category.Id)
				.Select(g => g.First().Category)
				.OrderBy(c => c.Name)
				.ToList();
			ViewData["Categories"] = allCategories;
			if (categoryId.HasValue)
			{
				ViewData["ActiveCategory"] = products.Select(p => p.Category).FirstOrDefault(c => c != null && c.Id == categoryId.Value);
			}

			return View(pagedProducts);
		}

		public IActionResult SetLanguage(string culture, string returnUrl)
		{
			// seo-canonical-hreflang 2.3: swap the culture segment only, so the path remainder and
			// the full query string survive — including a culture token inside a query value.
			returnUrl = CultureHelper.SwapCultureSegment(returnUrl, culture);
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
	[HttpPost]
	[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
	public IActionResult SetPreviewMode(string mode, string? returnUrl)
	{
		var normalized = mode?.Trim().ToLowerInvariant();
			if (normalized is "wholesale" or "retail")
			{
				HttpContext.Session.SetString(SD.AdminPreviewMode, normalized);
			}
			if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
			return RedirectToAction(nameof(Index));
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
