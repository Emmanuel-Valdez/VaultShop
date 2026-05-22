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
	public class PackagingController : Controller
	{
		public readonly IUnitOfWork _unitOfWork;
		private readonly IStringLocalizer<PackagingController> _localizer;
		public PackagingController(IUnitOfWork unitOfWork, IStringLocalizer<PackagingController> localizer)
		{
			_unitOfWork = unitOfWork;
			_localizer = localizer;
		}
		public IActionResult Index()
		{
			var objPackagingList = _unitOfWork.Packaging.GetAll().ToList();
			return View(objPackagingList);
		}
		public IActionResult Upsert(int? id)
		{
			if (id == 0 || id == null)
			{
				Packaging packaging = new();
				return View(packaging);
			}
			else
			{
				Packaging? packagingFromDb = _unitOfWork.Packaging.Get(u => u.Id == id);
				if (packagingFromDb == null)
					return NotFound();
				return View(packagingFromDb);
			}
		}
		[HttpPost]
		public IActionResult Upsert(Packaging obj)
		{

			if (!ModelState.IsValid)
				return View(obj);

			
			if (obj.Id == 0)
			{
				_unitOfWork.Packaging.Add(obj);
				TempData["success"] = _localizer["PackagingCreatedSuccesfully"].Value;
			}
			else
			{
				_unitOfWork.Packaging.Update(obj);
				TempData["success"] = _localizer["PackagingEditedSuccesfully"].Value;
			}
			_unitOfWork.Save();
			return RedirectToAction("Index");

		}

		#region API CALLS
		[HttpGet]
		public IActionResult GetAll()
		{
			List<Packaging> objPackagingList = _unitOfWork.Packaging.GetAll().ToList();
			return Json(new { data = objPackagingList });

		}
		[HttpPost]
		public IActionResult Delete(int? id)
		{
			Packaging packagingToBeDeleted = _unitOfWork.Packaging.Get(u => u.Id == id);
			if (packagingToBeDeleted == null)
			{
				return Json(new { success = false, message = _localizer["ErrorWhileDeleting"].Value });
			}
			_unitOfWork.Packaging.Remove(packagingToBeDeleted);
			_unitOfWork.Save();
			return Ok(new { success = true, message = _localizer["DeleteSuccesfully"].Value});
		}
		#endregion
	}
}
