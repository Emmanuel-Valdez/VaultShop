using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using VaultShop.Utility;
using VaultShop.Web.Services.ProductVariants;

namespace VaultShop.Web.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
	public class ProductVariantController : Controller
	{
		private readonly IProductVariantService _variantService;
		private readonly IStringLocalizer<ProductVariantController> _localizer;

		public ProductVariantController(IProductVariantService variantService, IStringLocalizer<ProductVariantController> localizer)
		{
			_variantService = variantService;
			_localizer = localizer;
		}

		public IActionResult Manage(int productId)
		{
			var data = _variantService.GetAdminData(productId);
			if (data == null)
			{
				return NotFound();
			}

			return View(data);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult AddValue(int productId, string typeName, string value, int sortOrder = 0)
		{
			var result = _variantService.AddValue(productId, typeName, value, sortOrder);
			TempData[result.Success ? "success" : "error"] = result.Success
				? _localizer["VariantValueAdded"].Value
				: _localizer[result.ErrorKey ?? "UnexpectedError"].Value;
			return RedirectToAction(nameof(Manage), new { productId });
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult DeleteValue(int productId, int valueId)
		{
			var result = _variantService.DeleteValue(productId, valueId);
			TempData[result.Success ? "success" : "error"] = result.Success
				? _localizer["VariantValueDeleted"].Value
				: _localizer[result.ErrorKey ?? "UnexpectedError"].Value;
			return RedirectToAction(nameof(Manage), new { productId });
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult Generate(int productId)
		{
			var result = _variantService.GenerateCombinations(productId);
			TempData[result.Success ? "success" : "error"] = result.Success
				? _localizer["CombinationsGenerated", result.CreatedCount].Value
				: _localizer[result.ErrorKey ?? "UnexpectedError"].Value;
			return RedirectToAction(nameof(Manage), new { productId });
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult SetAvailability(int productId, int variantId, bool isAvailable)
		{
			var result = _variantService.SetAvailability(productId, variantId, isAvailable);
			TempData[result.Success ? "success" : "error"] = result.Success
				? _localizer["VariantAvailabilityUpdated"].Value
				: _localizer[result.ErrorKey ?? "UnexpectedError"].Value;
			return RedirectToAction(nameof(Manage), new { productId });
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult DeleteVariant(int productId, int variantId)
		{
			var result = _variantService.DeleteVariant(productId, variantId);
			TempData[result.Success ? "success" : "error"] = result.Success
				? _localizer["VariantDeleted"].Value
				: _localizer[result.ErrorKey ?? "UnexpectedError"].Value;
			return RedirectToAction(nameof(Manage), new { productId });
		}
	}
}
