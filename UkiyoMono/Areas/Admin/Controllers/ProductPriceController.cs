using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Globalization;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models;
using UkiyoDesigns.Models.CalculatorModels;
using UkiyoDesigns.Models.CalculatorModels.SQLViews;
using UkiyoDesigns.Models.ViewModels;
using UkiyoDesigns.Utility;

namespace UkiyoDesignsWeb.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
	public class ProductPriceController : Controller
	{
		public readonly IUnitOfWork _unitOfWork;
		private readonly IStringLocalizer<ProductPriceController> _localizer;

		[BindProperty]
		public FinalPriceVM FinalPriceVM { get; set; } = null!;
		public ProductPriceController(IUnitOfWork unitOfWork, IStringLocalizer<ProductPriceController> localizer)
		{
			_unitOfWork = unitOfWork;
			_localizer = localizer;
		}
		public IActionResult Index()
		{
			var totalPercentageCost = _unitOfWork.TotalPercentageCost.GetAll().FirstOrDefault();
			var percentageProfit = _unitOfWork.PercentageProfit.Get(u => u.Id == 1);
			if (totalPercentageCost == null || percentageProfit == null)
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
			List<Product> actualProductList = _unitOfWork.Product.GetAll(u => u.IsDeleted == false).ToList();
			List<FinalPriceView> objPriceCalculated = _unitOfWork.FinalPriceView.GetAll().ToList();
			var productsOutdated = actualProductList.Where(p => objPriceCalculated
				.Any(c => c.Id == p.Id && (!ArePricesEqual(c.FinalRetail, p.ListPrice) || !ArePricesEqual(c.WholesaleWithProfit, p.FinalWholesalePrice)))).ToList();
			if (productsOutdated.Any())
			{
				FinalPriceVM.CountOutdated = productsOutdated.Count;
			}
			else
			{
				FinalPriceVM.CountOutdated = 0;
			}
		}

		[HttpPost]
		public IActionResult UpdatePrices()
		{
			List<Product> actualProductList = _unitOfWork.Product.GetAll(u => u.IsDeleted == false).ToList();
			List<FinalPriceView> objPriceInView = _unitOfWork.FinalPriceView.GetAll().ToList();
			var productsOutdated = actualProductList.Where(p => objPriceInView
			.Any(c => c.Id == p.Id &&
			(!ArePricesEqual(c.FinalRetail, p.ListPrice) ||
			!ArePricesEqual(c.WholesaleWithProfit, p.FinalWholesalePrice)))).ToList();
			foreach (var productOutdated in productsOutdated)
			{
				var productNewPrice = objPriceInView.First(u => u.Id == productOutdated.Id);
				productOutdated.ListPrice = productNewPrice.FinalRetail;
				productOutdated.FinalWholesalePrice = productNewPrice.WholesaleWithProfit;
			}
			_unitOfWork.Product.UpdateRange(productsOutdated);
			_unitOfWork.Save();
			TempData["success"] = $"{FinalPriceVM.CountOutdated} {_localizer["Product"].Value}{(FinalPriceVM.CountOutdated > 1 ? 's' : ' ')} {_localizer["UpdatedSuccessfully"].Value} ";
			return RedirectToAction(nameof(Index));

		}

		private static bool ArePricesEqual(decimal price1, decimal price2, decimal tolerance = 0.02m)
		{
			return Math.Abs(price1 - price2) <= tolerance;
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
			PercentageProfit? percentageProfitFromDb = _unitOfWork.PercentageProfit.Get(u => u.Id == 1);
			if (percentageProfitFromDb == null)
			{
				return NotFound();
			}

			percentageProfitFromDb.Retail = FinalPriceVM.PercentageProfit.Retail;
			percentageProfitFromDb.Wholesale = FinalPriceVM.PercentageProfit.Wholesale;
			_unitOfWork.PercentageProfit.Update(percentageProfitFromDb);
			_unitOfWork.Save();
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
			List<CostByProductView> objCostByProductViewList = _unitOfWork.CostByProductView.GetAll(includeProperties: "Product").ToList();

			return Json(new { data = objCostByProductViewList });

		}

		[HttpGet]
		public IActionResult GetAllProductFinalPrice()
		{
			List<FinalPriceView> objFinalPriceViewList = _unitOfWork.FinalPriceView.GetAll(includeProperties: "Product").ToList();

			return Json(new { data = objFinalPriceViewList });

		}
		#endregion
	}
}
