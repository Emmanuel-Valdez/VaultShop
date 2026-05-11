using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models;
using UkiyoDesigns.Utility;

namespace UkiyoDesignsWeb.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
	public class CompanyController : Controller
	{
		public readonly IUnitOfWork _unitOfWork;
		private readonly IStringLocalizer<CompanyController> _localizer;

	
		public CompanyController(IUnitOfWork unitOfWork, IStringLocalizer<CompanyController> localizer)
		{
			_unitOfWork = unitOfWork;
			_localizer = localizer;
		}
		public IActionResult Index()
		{
			var objCompanyList = _unitOfWork.Company.GetAll().ToList();
			return View(objCompanyList);
		}
		public IActionResult Upsert(int? id)
		{
			if (id == 0 || id == null)
			{
				Company company = new();
				return View(company);
			}
			else
			{
				//Company? companyFromDb = _db.Categories.Find(id);
				//Company? companyFromDb2 = _db.Categories.Where(u => u.Id == id).FirstOrDefault();
				Company? companyFromDb = _unitOfWork.Company.Get(u => u.Id == id);
				if (companyFromDb == null)
					return NotFound();
				return View(companyFromDb);
			}
		}
		[HttpPost]
		public IActionResult Upsert(Company obj)
		{

			if (!ModelState.IsValid)
			 return View(obj); 

			if (obj.Id == 0)
			{
				_unitOfWork.Company.Add(obj);
				TempData["success"] = _localizer["CompanyCreatedSuccesfully"].Value;
			}
			else
			{
				_unitOfWork.Company.Update(obj);
				TempData["success"] = _localizer["CompanyEditedSuccesfully"].Value;
			}
			_unitOfWork.Save();

			return RedirectToAction("Index");
		}

		#region API CALLS
		[HttpGet]
		public IActionResult GetAll()
		{
			List<Company> objCompanyList = _unitOfWork.Company.GetAll().ToList();
			return Json(new { data = objCompanyList });

		}
		[HttpDelete]
		public IActionResult Delete(int? id)
		{
			Company companyToBeDeleted = _unitOfWork.Company.Get(u => u.Id == id);
			if (companyToBeDeleted == null)
			{
				return Json(new { success = false, message = _localizer["ErrorWhileDeleting"].Value});
			}
			_unitOfWork.Company.Remove(companyToBeDeleted);
			_unitOfWork.Save();
			return Ok(new { success = true, message = _localizer["DeleteSuccesfully"].Value });

		}


		#endregion
	}
}
