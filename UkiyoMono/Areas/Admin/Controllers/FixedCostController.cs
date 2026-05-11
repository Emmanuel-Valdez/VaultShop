using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Localization;
using System.Globalization;
using UkiyoDesigns.DataAccess.Repository.IRepository;

using UkiyoDesigns.Models.CalculatorModels;
using UkiyoDesigns.Models.CalculatorModels.SQLViews;
using UkiyoDesigns.Utility;

namespace UkiyoDesignsWeb.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
	public class FixedCostController : Controller
	{
		public readonly IUnitOfWork _unitOfWork;
		private readonly IStringLocalizer<FixedCostController> _localizer;

		public FixedCostController(IUnitOfWork unitOfWork, IStringLocalizer<FixedCostController> localizer)
		{
			_unitOfWork = unitOfWork;
			_localizer = localizer;
		}
		public IActionResult Index()
		{
			var objTotalMonthly = _unitOfWork.FixedCostMonthly.GetAll().FirstOrDefault();

			return View(objTotalMonthly);
		}
		public IActionResult Upsert(int? id)
		{
			if (id == 0 || id == null)
			{
				FixedCost fabric = new();
				return View(fabric);
			}
			else
			{
				FixedCost? fabricFromDb = _unitOfWork.FixedCost.Get(u => u.Id == id);
				if (fabricFromDb == null)
					return NotFound();
				return View(fabricFromDb);
			}
		}
		[HttpPost]
		public IActionResult Upsert(FixedCost obj)
		{
			if (!ModelState.IsValid)
				return View(obj);
			if (obj.Id == 0)
			{
				_unitOfWork.FixedCost.Add(obj);
				TempData["success"] = _localizer["FixedCostCreatedSuccesfully"].Value;
			}
			else
			{
				_unitOfWork.FixedCost.Update(obj);
				TempData["success"] = _localizer["FixedCostEditedSuccesfully"].Value;
			}
			_unitOfWork.Save();

			return RedirectToAction("Index");
		}

		#region API CALLS
		[HttpGet]
		public IActionResult GetAll()
		{
			List<FixedCost> objFixedCostList = _unitOfWork.FixedCost.GetAll().ToList();
			return Json(new { data = objFixedCostList });

		}
		[HttpDelete]
		public IActionResult Delete(int? id)
		{
			FixedCost fabricToBeDeleted = _unitOfWork.FixedCost.Get(u => u.Id == id);
			if (fabricToBeDeleted == null)
			{
				return Json(new { success = false, message = _localizer["ErrorWhileDeleting"].Value });
			}

			_unitOfWork.FixedCost.Remove(fabricToBeDeleted);
			_unitOfWork.Save();
			return Json(new { success = true, message = _localizer["DeleteSuccesfully"].Value });

		}


		#endregion
	}
}
