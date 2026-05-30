using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
		private readonly UserManager<ApplicationUser> _userManager;

	
		public CompanyController(IUnitOfWork unitOfWork, IStringLocalizer<CompanyController> localizer, UserManager<ApplicationUser> userManager)
		{
			_unitOfWork = unitOfWork;
			_localizer = localizer;
			_userManager = userManager;
		}
		public IActionResult Index()
		{
			var objCompanyList = _unitOfWork.Company.GetAll(u => u.IsDeleted == false).ToList();
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
				Company? companyFromDb = _unitOfWork.Company.Get(u => u.Id == id && u.IsDeleted == false);
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
				obj.IsDeleted = false;
				_unitOfWork.Company.Add(obj);
				TempData["success"] = _localizer["CompanyCreatedSuccesfully"].Value;
			}
			else
			{
				Company? companyFromDb = _unitOfWork.Company.Get(u => u.Id == obj.Id && u.IsDeleted == false);
				if (companyFromDb == null)
				{
					return NotFound();
				}

				companyFromDb.Name = obj.Name;
				companyFromDb.StreetAddress = obj.StreetAddress;
				companyFromDb.City = obj.City;
				companyFromDb.State = obj.State;
				companyFromDb.PostalCode = obj.PostalCode;
				companyFromDb.PhoneNumber = obj.PhoneNumber;
				_unitOfWork.Company.Update(companyFromDb);
				TempData["success"] = _localizer["CompanyEditedSuccesfully"].Value;
			}
			_unitOfWork.Save();

			return RedirectToAction("Index");
		}

		#region API CALLS
		[HttpGet]
		public IActionResult GetAll()
		{
			List<Company> objCompanyList = _unitOfWork.Company.GetAll(u => u.IsDeleted == false).ToList();
			return Json(new { data = objCompanyList });

		}
		[HttpDelete]
		public async Task<IActionResult> Delete(int? id)
		{
			Company? companyToBeDeleted = _unitOfWork.Company.Get(u => u.Id == id && u.IsDeleted == false);
			if (companyToBeDeleted == null)
			{
				return Json(new { success = false, message = _localizer["ErrorWhileDeleting"].Value});
			}
			companyToBeDeleted.IsDeleted = true;
			_unitOfWork.Company.Update(companyToBeDeleted);

			List<ApplicationUser> companyUsers = _unitOfWork.ApplicationUser
				.GetAll(u => u.CompanyId == companyToBeDeleted.Id)
				.ToList();
			foreach (ApplicationUser user in companyUsers)
			{
				user.LockoutEnabled = true;
				user.LockoutEnd = DateTime.Now.AddYears(1000);
				_unitOfWork.ApplicationUser.Update(user);
			}

			_unitOfWork.Save();
			foreach (ApplicationUser user in companyUsers)
			{
				await _userManager.UpdateSecurityStampAsync(user);
			}

			return Ok(new { success = true, message = _localizer["DeleteSuccesfully"].Value });

		}


		#endregion
	}
}
