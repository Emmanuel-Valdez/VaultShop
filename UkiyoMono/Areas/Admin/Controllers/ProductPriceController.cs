using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using UkiyoDesigns.Models.CalculatorModels;
using UkiyoDesigns.Models.CalculatorModels.SQLViews;
using UkiyoDesigns.Models.ViewModels;
using UkiyoDesigns.Utility;
using UkiyoDesignsWeb.Services.Pricing;

namespace UkiyoDesignsWeb.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
	public class ProductPriceController : Controller
	{
		private readonly IPricingCalculatorService _pricingCalculatorService;
		private readonly IStringLocalizer<ProductPriceController> _localizer;

		[BindProperty]
		public FinalPriceVM FinalPriceVM { get; set; } = null!;
		public ProductPriceController(IPricingCalculatorService pricingCalculatorService, IStringLocalizer<ProductPriceController> localizer)
		{
			_pricingCalculatorService = pricingCalculatorService;
			_localizer = localizer;
		}
		public IActionResult Index()
		{
			var totalPercentageCost = _pricingCalculatorService.GetTotalPercentageCost();
			var percentageProfit = _pricingCalculatorService.GetPercentageProfit();
			if (percentageProfit == null)
			{
				return Problem("Price calculator data is not initialized.");
			}

			FinalPriceVM = new FinalPriceVM()
			{
				TotalPercentageCost = totalPercentageCost,
				PercentageProfit = percentageProfit,
			};
			ProductsOutdatedCount();
			return View(FinalPriceVM);
		}

		public void ProductsOutdatedCount()
		{
			FinalPriceVM.CountOutdated = _pricingCalculatorService.GetOutdatedProductCount();
		}

		[HttpPost]
		public IActionResult UpdatePrices()
		{
			var updatedCount = _pricingCalculatorService.PublishSuggestedPrices();
			var messageKey = updatedCount == 1 ? "ProductPriceUpdatedSingular" : "ProductPriceUpdatedPlural";
			TempData["success"] = string.Format(_localizer[messageKey].Value, updatedCount);
			return RedirectToAction(nameof(Index));

		}

		[HttpPost]
		[ActionName("Index")]
		public IActionResult UpdateProfits()
		{
			if (!ModelState.IsValid)
			{
				ProductsOutdatedCount();
				return View(FinalPriceVM);
			}
			PercentageProfit? percentageProfitFromDb = _pricingCalculatorService.GetPercentageProfit();
			if (percentageProfitFromDb == null)
			{
				return NotFound();
			}

			_pricingCalculatorService.UpdatePercentageProfit(FinalPriceVM.PercentageProfit.Retail, FinalPriceVM.PercentageProfit.Wholesale);
			ProductsOutdatedCount();
			return View(FinalPriceVM);
		}

		public IActionResult CostByProduct()
		{
			return View();
		}

		#region API CALLS
		[HttpGet]
		public IActionResult GetAllCostByProduct()
		{
			List<CostByProductView> objCostByProductViewList = _pricingCalculatorService.GetCostByProducts().ToList();

			return Json(new { data = objCostByProductViewList });

		}

		[HttpGet]
		public IActionResult GetAllProductFinalPrice()
		{
			List<FinalPriceView> objFinalPriceViewList = _pricingCalculatorService.GetFinalPrices().ToList();

			return Json(new { data = objFinalPriceViewList });

		}
		#endregion
	}
}

