using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Globalization;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Models.CalculatorModels;
using VaultShop.Utility;

namespace VaultShop.Web.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
	public class CategoryController : Controller
	{
		public readonly IUnitOfWork _unitOfWork;
		private readonly IStringLocalizer<CategoryController> _localizer;

		public CategoryController(IUnitOfWork unitOfWork, IStringLocalizer<CategoryController> localizer)
		{
			_unitOfWork = unitOfWork;
			_localizer = localizer;
		}
		public IActionResult Index()
		{

			return View();
		}
		public IActionResult Upsert(int? id)
		{
			if (id == 0 || id == null)
			{
				Category category = new();
				return View(category);
			}
			else
			{
				//Category? categoryFromDb = _db.Categories.Find(id);
				//Category? categoryFromDb2 = _db.Categories.Where(u => u.Id == id).FirstOrDefault();
				Category? categoryFromDb = _unitOfWork.Category.Get(u => u.Id == id && u.IsDeleted == false, includeProperties: "PackagingByCategory");
				if (categoryFromDb == null)
					return NotFound();
				return View(categoryFromDb);
			}
		}
		[HttpPost]
		public IActionResult Upsert(Category obj)
		{
			if (obj.Name == obj.AvgShippingCost.ToString() || obj.Name == obj.MaxExpectation.ToString())
			{
				ModelState.AddModelError("name", _localizer["NameError"].Value);
			}
			if (obj.Name != null && obj.Name.ToLower() == "test")
			{
				ModelState.AddModelError("", _localizer["NameCantBeTest"].Value);
			}

			if (!ModelState.IsValid)
				return View(obj);

			if (obj.Id == 0)
			{
				obj.PackagingByCategory = new();
				_unitOfWork.Category.Add(obj);
				_unitOfWork.Save();

				TempData["success"] = _localizer["CategoryCreatedSuccess"].Value;
			}
			else
			{
				_unitOfWork.Category.Update(obj);
				TempData["success"] = _localizer["CategoryEditedSuccess"].Value;
			}
			_unitOfWork.Save();
			return RedirectToAction("Index");

		}

		#region API CALLS
		[HttpGet]
		public IActionResult GetAll()
		{
			List<Category> objCategoryList = _unitOfWork.Category.GetAll(u => u.IsDeleted == false).ToList();
			return Json(new { data = objCategoryList });

		}
		[HttpPost]
		public IActionResult Delete(int? id)
		{
			Category? categoryToBeDeleted = _unitOfWork.Category.Get(u => u.Id == id && u.IsDeleted == false);
			if (categoryToBeDeleted == null)
			{
				return Json(new { success = false, message = _localizer["ErrorWhileDeleting"].Value });
			}
			categoryToBeDeleted.IsDeleted = true;
			_unitOfWork.Category.Update(categoryToBeDeleted);
			_unitOfWork.Save();
			return Json(new { success = true, message = _localizer["DeleteSuccesfully"].Value });

		}


		#endregion
	}
}
