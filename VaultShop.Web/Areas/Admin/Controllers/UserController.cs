using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Models.ViewModels;
using VaultShop.Utility;

namespace VaultShop.Web.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]

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

			var currentRole = _userManager.GetRolesAsync(applicationUser)
				.GetAwaiter().GetResult().FirstOrDefault() ?? string.Empty;
			if (!CanEmployeeManageRole(currentRole))
			{
				return NotFound();
			}

			RoleManagmentVM RoleVM = new RoleManagmentVM()
			{
				ApplicationUser = applicationUser,
				RoleList = GetAssignableRoleList(),

				CompanyList = _unitOfWork.Company.GetAll(u => u.IsDeleted == false).Select(u => new SelectListItem
				{
					Text = u.Name,
					Value = u.Id.ToString()
				})
			};
			RoleVM.ApplicationUser.Role = currentRole;
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
			if (!CanEmployeeManageRole(oldRole) || !CanAssignRole(roleManagmentVM.ApplicationUser.Role))
			{
				return NotFound();
			}

			if (roleManagmentVM.ApplicationUser.Role != SD.Role_Company)
			{
				roleManagmentVM.ApplicationUser.CompanyId = null;
			}
			else if (!_unitOfWork.Company.GetAll(u => u.IsDeleted == false && u.Id == roleManagmentVM.ApplicationUser.CompanyId).Any())
			{
				return NotFound();
			}

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
			if (User.IsInRole(SD.Role_Employee))
			{
				objUserList = objUserList
					.Where(u => u.Role != SD.Role_Admin && u.Role != SD.Role_Employee)
					.ToList();
			}
			return Json(new { data = objUserList });
		}

		[HttpPost]
		public IActionResult LockUnlock([FromBody] string id)
		{

			var objFromDb = _unitOfWork.ApplicationUser.Get(u => u.Id == id);

			if (objFromDb == null)
			{
				return Json(new { success = true, message = _localizer["ErrorLockUnlock"].Value });
			}
			var targetRole = _userManager.GetRolesAsync(objFromDb).GetAwaiter().GetResult().FirstOrDefault() ?? string.Empty;
			if (!CanEmployeeManageRole(targetRole))
			{
				return NotFound();
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

		private IEnumerable<SelectListItem> GetAssignableRoleList()
		{
			var roles = _roleManager.Roles.Select(u => u.Name ?? string.Empty);
			if (User.IsInRole(SD.Role_Employee))
			{
				roles = roles.Where(u => u == SD.Role_Customer || u == SD.Role_Company);
			}

			return roles.Select(u => new SelectListItem
			{
				Text = u,
				Value = u
			});
		}

		private bool CanEmployeeManageRole(string role)
		{
			return User.IsInRole(SD.Role_Admin)
				|| (role != SD.Role_Admin && role != SD.Role_Employee);
		}

		private bool CanAssignRole(string role)
		{
			return User.IsInRole(SD.Role_Admin)
				|| role == SD.Role_Customer
				|| role == SD.Role_Company;
		}


		#endregion
	}
}
