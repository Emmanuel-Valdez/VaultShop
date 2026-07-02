using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models.CalculatorModels;
using UkiyoDesigns.Utility;
using UkiyoDesignsWeb.Services.Pricing;
using UkiyoDesignsWeb.Services.RichText;

namespace UkiyoDesignsWeb.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
	public class PercentageCostWholesaleController : Controller
	{
		public readonly IUnitOfWork _unitOfWork;
		private readonly IPricingCalculatorService _pricingCalculatorService;
		private readonly IStringLocalizer<PercentageCostWholesaleController> _localizer;
		private readonly IRichTextSanitizer _richTextSanitizer;

		public PercentageCostWholesaleController(IUnitOfWork unitOfWork, IPricingCalculatorService pricingCalculatorService, IStringLocalizer<PercentageCostWholesaleController> localizer, IRichTextSanitizer richTextSanitizer)
		{
			_localizer = localizer;
			_unitOfWork = unitOfWork;
			_pricingCalculatorService = pricingCalculatorService;
			_richTextSanitizer = richTextSanitizer;
		}

		public IActionResult Index()
		{
			var objTotalPercentage = _pricingCalculatorService.GetTotalPercentageCostWholesale();
			return View(objTotalPercentage);
		}

		public IActionResult Upsert(int? id)
		{
			if (id == 0 || id == null)
			{
				PercentageCostWholesale percentage = new();
				return View(percentage);
			}

			PercentageCostWholesale? percentageFromDb = _unitOfWork.PercentageCostWholesale.Get(u => u.Id == id);
			if (percentageFromDb == null)
			{
				return NotFound();
			}

			return View(percentageFromDb);
		}

		[HttpPost]
		public IActionResult Upsert(PercentageCostWholesale obj)
		{
			obj.Description = _richTextSanitizer.Sanitize(obj.Description);
			ModelState.Remove(nameof(PercentageCostWholesale.Description));

			if (!ModelState.IsValid)
			{
				return View(obj);
			}

			if (obj.Id == 0)
			{
				_unitOfWork.PercentageCostWholesale.Add(obj);
				TempData["success"] = _localizer["PercentageCostWholesaleCreatedSuccesfully"].Value;
			}
			else
			{
				_unitOfWork.PercentageCostWholesale.Update(obj);
				TempData["success"] = _localizer["PercentageCostWholesaleEditedSuccesfully"].Value;
			}

			_unitOfWork.Save();
			return RedirectToAction("Index");
		}

		#region API CALLS
		[HttpGet]
		public IActionResult GetAll()
		{
			List<PercentageCostWholesale> objPercentageCostList = _unitOfWork.PercentageCostWholesale.GetAll().ToList();
			return Json(new { data = objPercentageCostList });
		}

		[HttpPost]
		public IActionResult Delete(int? id)
		{
			PercentageCostWholesale? percentageToBeDeleted = _unitOfWork.PercentageCostWholesale.Get(u => u.Id == id);
			if (percentageToBeDeleted == null)
			{
				return Json(new { success = false, message = _localizer["ErrorWhileDeleting"] });
			}

			_unitOfWork.PercentageCostWholesale.Remove(percentageToBeDeleted);
			_unitOfWork.Save();
			var updatedTotalPercentage = _pricingCalculatorService.GetTotalPercentageCostWholesale().TotalPercentage;
			return Json(new { success = true, message = _localizer["DeleteSuccesfully"], totalPercentage = updatedTotalPercentage });
		}
		#endregion
	}
}
