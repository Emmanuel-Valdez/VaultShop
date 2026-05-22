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
	public class GarmentHardwareController : Controller
	{
		public readonly IUnitOfWork _unitOfWork;
		private readonly IStringLocalizer<GarmentHardwareController> _localizer; 

		public GarmentHardwareController(IUnitOfWork unitOfWork, IStringLocalizer<GarmentHardwareController> localizer)
		{
			_localizer = localizer;
			_unitOfWork = unitOfWork;
		}
		public IActionResult Index()
		{
			var objFabricList = _unitOfWork.Fabric.GetAll().ToList();
			return View(objFabricList);
		}
		public IActionResult Upsert(int? id)
		{
			if (id == 0 || id == null)
			{
				GarmentHardware garmentHardware = new();
				return View(garmentHardware);
			}
			else
			{
				GarmentHardware? garmentHardwareFromDb = _unitOfWork.GarmentHardware.Get(u => u.Id == id);
				if (garmentHardwareFromDb == null)
					return NotFound();
				return View(garmentHardwareFromDb);
			}
		}
		[HttpPost]
		public IActionResult Upsert(GarmentHardware obj)
		{
			if (!ModelState.IsValid)
				return View(obj);

		
			if (obj.Id == 0)
			{
				_unitOfWork.GarmentHardware.Add(obj);
				TempData["success"] = _localizer["GarmentHardwareCreatedSuccesfully"].Value;
			}
			else
			{
				_unitOfWork.GarmentHardware.Update(obj);
				TempData["success"] = _localizer["GarmentHardwareEditedSuccesfully"].Value;
			}
			_unitOfWork.Save();

			return RedirectToAction("Index");
		}

		#region API CALLS
		[HttpGet]
		public IActionResult GetAll()
		{
			List<GarmentHardware> objGarmentHardwareList = _unitOfWork.GarmentHardware.GetAll().ToList();
			return Json(new { data = objGarmentHardwareList });

		}
		[HttpPost]
		public IActionResult Delete(int? id)
		{
			GarmentHardware garmentHardwareToBeDeleted = _unitOfWork.GarmentHardware.Get(u => u.Id == id);
			if (garmentHardwareToBeDeleted == null)
			{
				return Json(new { success = false, message = _localizer["ErrorWhileDeleting"].Value });
			}

			_unitOfWork.GarmentHardware.Remove(garmentHardwareToBeDeleted);
			_unitOfWork.Save();
			return Json(new { success = true, message = _localizer["DeleteSuccesfully"].Value });

		}


		#endregion
	}
}
