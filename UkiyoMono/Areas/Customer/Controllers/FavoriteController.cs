using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Security.Claims;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models;
using UkiyoDesigns.Models.ViewModels;

namespace UkiyoDesignsWeb.Areas.Customer.Controllers
{
	[Area("Customer")]
	[Authorize]
	public class FavoriteController : Controller
	{
		private readonly ILogger<HomeController> _logger;
		private readonly IUnitOfWork _unitOfWork;
		private readonly IStringLocalizer _localizer;
		public FavoriteController(ILogger<HomeController> logger, IUnitOfWork unitOfWork, IStringLocalizer<FavoriteController> localizer)
		{
			_localizer = localizer;
			_logger = logger;
			_unitOfWork = unitOfWork;
		}
		
		public IActionResult Index()
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized();
			}

			IEnumerable<FavoriteProduct> FavoriteProductList = _unitOfWork.FavoriteProduct
					.GetAll(u => u.Product.IsDeleted == false && u.Product.IsAvailableInStore == true && userId == u.ApplicationUserId
					, includeProperties: "Product");
			IEnumerable<ProductImage> productImages = _unitOfWork.ProductImage.GetAll();
			foreach (var item in FavoriteProductList)
			{
				item.Product.ProductImages = productImages.Where(u => u.ProductId == item.Product.Id).ToList();
			}

			return View(FavoriteProductList);
		}
		
		[HttpPost]
		public IActionResult Add(int productId)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (string.IsNullOrEmpty(userId))
			{
				return Unauthorized();
			}

			var product = _unitOfWork.Product.Get(u => u.Id == productId && u.IsDeleted == false && u.IsAvailableInStore == true);
			if (product == null)
			{
				TempData["error"] = _localizer["ProductUnavailable"].Value;
				return RedirectToAction("Index", "Home");
			}

			var existingFavorite = _unitOfWork.FavoriteProduct.Get(u => u.ProductId == productId && u.ApplicationUserId == userId);
			if (existingFavorite != null)
			{
				TempData["success"] = _localizer["FavoriteAlreadyAdded"].Value;
				return RedirectToAction("Details", "Home", new { productId });
			}

			FavoriteProduct favoriteProduct = new()
			{
				ProductId = productId,
				ApplicationUserId = userId
			};
			_unitOfWork.FavoriteProduct.Add(favoriteProduct);
			_unitOfWork.Save();
			TempData["Success"] = _localizer["FavoriteAdd"].Value;

			return RedirectToAction("Details","Home", new { productId });
		}
		[HttpPost]
		public IActionResult RemoveFromIndex(int favoriteId)
		{
			var result = Remove(favoriteId);
			if (result is StatusCodeResult statusCodeResult && statusCodeResult.StatusCode == 204)
			{
				return RedirectToAction(nameof(Index));
			}
			return result;
		}

		[HttpPost]
		public IActionResult RemoveFromProductDetails(int favoriteId, int productId)
		{
			var result = Remove(favoriteId);
			if (result is StatusCodeResult statusCodeResult && statusCodeResult.StatusCode == 204)
			{
				return RedirectToAction("Details", "Home", new { productId });
			}
			return result;
		}

		private IActionResult Remove(int favoriteId)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

			if (string.IsNullOrEmpty(userId))
			{
				return StatusCode(401, new { success = false, message = _localizer["UserNotAutenticated"].Value });
			}

			var favoriteProduct = _unitOfWork.FavoriteProduct.Get(u => u.Id == favoriteId && u.ApplicationUserId == userId);

			if (favoriteProduct == null)
			{
				TempData["error"] = _localizer["FavoriteNotFound"].Value;
				return StatusCode(404, new { success = false, message = _localizer[""].Value });
			}

			_unitOfWork.FavoriteProduct.Remove(favoriteProduct);
			_unitOfWork.Save();
			TempData["success"] = _localizer["FavoriteDeleted"].Value;
			return StatusCode(204);
		}

		





	}
}
