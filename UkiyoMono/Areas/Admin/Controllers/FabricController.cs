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
	public class FabricController : Controller
	{
		public readonly IUnitOfWork _unitOfWork;
		private readonly IStringLocalizer<FabricController> _localizer;
		public FabricController(IUnitOfWork unitOfWork, IStringLocalizer<FabricController> localizer)
		{
			_unitOfWork = unitOfWork;
			_localizer = localizer;
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
				Fabric fabric = new();
				return View(fabric);
			}
			else
			{
				//Fabric? fabricFromDb = _db.Fabrics.Find(id);
				//Fabric? fabricFromDb2 = _db.Fabrics.Where(u => u.Id == id).FirstOrDefault();
				Fabric? fabricFromDb = _unitOfWork.Fabric.Get(u => u.Id == id);
				if (fabricFromDb == null)
					return NotFound();
				return View(fabricFromDb);
			}
		}
		[HttpPost]
		public IActionResult Upsert(Fabric obj)
		{
			if (!ModelState.IsValid)
				return View(obj);

			
			if (obj.Id == 0)
			{
				_unitOfWork.Fabric.Add(obj);
				TempData["success"] = _localizer["FabricCreatedSuccesfully"].Value;
			}
			else
			{
				_unitOfWork.Fabric.Update(obj);
				TempData["success"] = _localizer["FabricEditedSuccesfully"].Value;
			}
			_unitOfWork.Save();

			return RedirectToAction("Index");
		}

		#region API CALLS
		[HttpGet]
		public IActionResult GetAll()
		{
			List<Fabric> objFabricList = _unitOfWork.Fabric.GetAll().ToList();
			return Json(new { data = objFabricList });

		}
		[HttpDelete]
		public IActionResult Delete(int? id)
		{
			Fabric fabricToBeDeleted = _unitOfWork.Fabric.Get(u => u.Id == id);
			if (fabricToBeDeleted == null)
			{
				return Json(new { success = false, message = _localizer["ErrorWhileDeleting"].Value });
			}

			_unitOfWork.Fabric.Remove(fabricToBeDeleted);
			_unitOfWork.Save();
			return Json(new { success = true, message = _localizer["DeleteSuccesfully"].Value });

		}


		#endregion
	}
}
