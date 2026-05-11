using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Globalization;
using UkiyoDesigns.DataAccess.Repository.IRepository;

using UkiyoDesigns.Models.CalculatorModels;
using UkiyoDesigns.Utility;

namespace UkiyoDesignsWeb.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]

	public class PercentageCostController : Controller
	{
		public readonly IUnitOfWork _unitOfWork;
		private readonly IStringLocalizer<PercentageCostController> _localizer;

		public PercentageCostController(IUnitOfWork unitOfWork, IStringLocalizer<PercentageCostController> localizer)
		{
			_localizer = localizer;
			_unitOfWork = unitOfWork;
		}
		public IActionResult Index()
		{
			var objTotalPercentage = _unitOfWork.TotalPercentageCost.GetAll().FirstOrDefault();
			return View(objTotalPercentage);
			
		}
		public IActionResult Upsert(int? id)
		{
			if (id == 0 || id == null)
			{
				PercentageCost percentage = new();
				return View(percentage);
			}
			else
			{
				PercentageCost? percentageFromDb = _unitOfWork.PercentageCost.Get(u => u.Id == id);
				if (percentageFromDb == null)
					return NotFound();
				return View(percentageFromDb);
			}
		}
		[HttpPost]
		public IActionResult Upsert(PercentageCost obj)
		{
			if (!ModelState.IsValid)
				return View(obj);
			if (obj.Id == 0)
			{
				_unitOfWork.PercentageCost.Add(obj);
				TempData["success"] = _localizer["PercentageCostCreatedSuccesfully"].Value;
			}
			else
			{
				_unitOfWork.PercentageCost.Update(obj);
				TempData["success"] = _localizer["PercentageCostEditedSuccesfully"].Value;
			}
			_unitOfWork.Save();

			return RedirectToAction("Index");
		}

		#region API CALLS
		[HttpGet]
		public IActionResult GetAll()
		{
			List<PercentageCost> objPercentageCostList = _unitOfWork.PercentageCost.GetAll().ToList();
			return Json(new { data = objPercentageCostList });

		}
		[HttpDelete]
		public IActionResult Delete(int? id)
		{
			PercentageCost percentageToBeDeleted = _unitOfWork.PercentageCost.Get(u => u.Id == id);
			if (percentageToBeDeleted == null)
			{
				return Json(new { success = false, message = _localizer["ErrorWhileDeleting"] });
			}

			_unitOfWork.PercentageCost.Remove(percentageToBeDeleted);
			_unitOfWork.Save();
			return Json(new { success = true, message = _localizer["DeleteSuccesfully"] });

		}


		#endregion
	}
}
