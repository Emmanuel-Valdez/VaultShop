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
			var claimsIdentity = (ClaimsIdentity)User.Identity;
			var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

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
		
		public IActionResult Add(int productId)
		{
			var claimsIdentity = (ClaimsIdentity)User.Identity;
			var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
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
		public IActionResult RemoveFromIndex(int favoriteId)
		{
			var result = Remove(favoriteId);
			if (result is StatusCodeResult statusCodeResult && statusCodeResult.StatusCode == 204)
			{
				return RedirectToAction(nameof(Index));
			}
			return result;
		}

		public IActionResult RemoveFromProductDetails(int favoriteId, int productId)
		{
			var result = Remove(favoriteId);
			if (result is StatusCodeResult statusCodeResult && statusCodeResult.StatusCode == 204)
			{
				return RedirectToAction("Details", "Home", new { productId });
			}
			return result;
		}

		public IActionResult Remove(int favoriteId)
		{
			var claimsIdentity = (ClaimsIdentity)User.Identity;
			var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier)?.Value;

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
