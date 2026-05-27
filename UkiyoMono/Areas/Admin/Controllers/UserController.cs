using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models;
using UkiyoDesigns.Models.ViewModels;
using UkiyoDesigns.Utility;

namespace UkiyoDesignsWeb.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = SD.Role_Admin)]

	public class UserController : Controller
	{
		private readonly UserManager<ApplicationUser> _userManager;
		private readonly RoleManager<IdentityRole> _roleManager;
		private readonly IUnitOfWork _unitOfWork;
		private readonly IStringLocalizer<UserController> _localizer;
		public UserController(IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,IStringLocalizer<UserController> localizer)
		{
			_userManager = userManager;
			_unitOfWork = unitOfWork;
			_roleManager = roleManager;
			_localizer = localizer;
		}
		public IActionResult Index()
		{
			return View();
		}

		public IActionResult RoleManagment(string userId)
		{
			var applicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId, includeProperties:"Company");
			if (applicationUser == null)
			{
				return NotFound();
			}

			RoleManagmentVM RoleVM = new RoleManagmentVM()
			{
				ApplicationUser = applicationUser,
				RoleList = _roleManager.Roles.Select(u => new SelectListItem
				{
					Text = u.Name,
					Value = u.Name
				}),

				CompanyList = _unitOfWork.Company.GetAll().Select(u => new SelectListItem
				{
					Text = u.Name,
					Value = u.Id.ToString()
				})
			};
			RoleVM.ApplicationUser.Role = _userManager.GetRolesAsync(applicationUser)
				.GetAwaiter().GetResult().FirstOrDefault() ?? string.Empty;
			return View(RoleVM);
		}
		[HttpPost]
		public IActionResult RoleManagment(RoleManagmentVM roleManagmentVM)
		{
			ApplicationUser? applicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == roleManagmentVM.ApplicationUser.Id);
			if (applicationUser == null)
			{
				return NotFound();
			}

			string oldRole = _userManager.GetRolesAsync(applicationUser)
				.GetAwaiter().GetResult().FirstOrDefault() ?? string.Empty;

			if (!(roleManagmentVM.ApplicationUser.Role == oldRole))
			{
				if (roleManagmentVM.ApplicationUser.Role == SD.Role_Company)
				{
					applicationUser.CompanyId = roleManagmentVM.ApplicationUser.CompanyId;
				}
				if (oldRole == SD.Role_Company)
				{
					applicationUser.CompanyId = null;
				}
				_unitOfWork.ApplicationUser.Update(applicationUser);
				_unitOfWork.Save();
				if (!string.IsNullOrEmpty(oldRole))
				{
					_userManager.RemoveFromRoleAsync(applicationUser, oldRole).GetAwaiter().GetResult();
				}

				_userManager.AddToRoleAsync(applicationUser, roleManagmentVM.ApplicationUser.Role).GetAwaiter().GetResult();
			}
			else if (roleManagmentVM.ApplicationUser.Role == SD.Role_Company 
				&& oldRole == SD.Role_Company &&
				applicationUser.CompanyId != roleManagmentVM.ApplicationUser.CompanyId)
			{
				
				applicationUser.CompanyId = roleManagmentVM.ApplicationUser.CompanyId;
				_unitOfWork.ApplicationUser.Update(applicationUser);
				_unitOfWork.Save();
			}
			return RedirectToAction("Index");
		}

		#region API CALLS
		[HttpGet]
		public IActionResult GetAll()
		{
			List<ApplicationUser> objUserList = _unitOfWork.ApplicationUser.GetAll(includeProperties:"Company").ToList();		
			foreach (var user in objUserList)
			{
				user.Role = _userManager.GetRolesAsync(user).GetAwaiter().GetResult().FirstOrDefault() ?? string.Empty;

				if (user.Company == null)
				{
					user.Company = new Company() { Name = "" };
				}
			}
			return Json(new { data = objUserList });
		}

		public IActionResult LockUnlock([FromBody] string id)
		{

			var objFromDb = _unitOfWork.ApplicationUser.Get(u => u.Id == id);

			if (objFromDb == null)
			{
				return Json(new { success = true, message = _localizer["ErrorLockUnlock"].Value });
			}

			if (objFromDb.LockoutEnd != null && objFromDb.LockoutEnd > DateTime.Now)
			{
				//user is currently locked and we nee to unlock them
				objFromDb.LockoutEnd = DateTime.Now;
			}
			else
			{
				objFromDb.LockoutEnd = DateTime.Now.AddYears(1000);
			}
			_unitOfWork.ApplicationUser.Update(objFromDb);
			_unitOfWork.Save();
			return Json(new { success = true, message = _localizer["OperationSuccessful"].Value}); 

		}


		#endregion
	}
}
