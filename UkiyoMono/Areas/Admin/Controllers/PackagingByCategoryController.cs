using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models;
using UkiyoDesigns.Models.CalculatorModels;
using UkiyoDesigns.Models.DTO;
using UkiyoDesigns.Models.ViewModels;
using UkiyoDesigns.Utility;
using UkiyoDesignsWeb.Services.Pricing;

namespace UkiyoDesignsWeb.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
	public class PackagingByCategoryController : Controller
	{
		public readonly IUnitOfWork _unitOfWork;
		private readonly IStringLocalizer<PackagingByCategoryController> _localizer;
		private readonly ILogger<PackagingByCategoryController> _logger;
		private readonly IPricingCalculatorService _pricingCalculatorService;
		[BindProperty]
		public PackagingByCategoryVM PackagingByCategoryVM { get; set; } = null!;

		public PackagingByCategoryController(IUnitOfWork unitOfWork, IStringLocalizer<PackagingByCategoryController> localizer, ILogger<PackagingByCategoryController> logger, IPricingCalculatorService pricingCalculatorService)
		{
			_unitOfWork = unitOfWork;
			_localizer = localizer;
			_logger = logger;
			_pricingCalculatorService = pricingCalculatorService;
		}
		public IActionResult Index()
		{

			return View();
		}
		public IActionResult Upsert(int id, int? unitId)
		{
			var unitFromDb = _unitOfWork.UnitPackagingByCategory
					.Get(u => u.Id == unitId && u.CategoryId == id);
			// It is revised to avoid error when deleting while in the editing view
			if (unitFromDb == null)
			{
				unitId = null;
			}
			var packagingByCategory = _unitOfWork.PackagingByCategory
				.Get(u => u.CategoryId == id, includeProperties: "Category,UnitPackagingByCategoryList");
			if (packagingByCategory == null)
			{
				return NotFound();
			}

			PackagingByCategoryVM = new()
			{
				PackagingList = _unitOfWork.Packaging.GetAll().Select(u => new SelectListItem
				{
					Text = u.Name,
					Value = u.Id.ToString()
				}),
				PackagingByCategory = packagingByCategory,
				CalculatedTotalByCategory = _pricingCalculatorService.GetPackagingTotalsByCategory().GetValueOrDefault(id),
			};
			if (unitId == 0 || unitId == null)
			{
				//create
				PackagingByCategoryVM.UnitPackagingByCategory = new();
				PackagingByCategoryVM.UnitPackagingByCategory.CategoryId = PackagingByCategoryVM.PackagingByCategory.CategoryId;
			}
			else
			{
				//update
				if (unitFromDb == null)
				{
					return NotFound();
				}

				PackagingByCategoryVM.UnitPackagingByCategory = unitFromDb;
			}
			return View(PackagingByCategoryVM);

		}

		[HttpPost]
		public IActionResult Upsert()
		{

			if (!ModelState.IsValid)
			{
				return View(PackagingByCategoryVM);
			}
			try
			{
				var packagingFromDb = _unitOfWork.Packaging.Get(u => u.Id == PackagingByCategoryVM.UnitPackagingByCategory.PackagingId);
				if (packagingFromDb == null)
				{
					return NotFound();
				}

				
				var existingUnitPackaging = _unitOfWork.UnitPackagingByCategory
					.GetAll(u => u.CategoryId == PackagingByCategoryVM.PackagingByCategory.CategoryId)
					.ToList();

				PackagingByCategoryVM.PackagingByCategory.UnitPackagingByCategoryList = existingUnitPackaging;

				var unitPackagingToUpdate = PackagingByCategoryVM.PackagingByCategory.UnitPackagingByCategoryList
						.FirstOrDefault(u => u.PackagingId == packagingFromDb.Id);

				if (unitPackagingToUpdate == null)
				{
					PackagingByCategoryVM.PackagingByCategory.UnitPackagingByCategoryList.Add(PackagingByCategoryVM.UnitPackagingByCategory);
					TempData["success"] = _localizer["UnitPackagingAddedSuccessfully"].Value ;
				}
				else
				{
					if(unitPackagingToUpdate.Id!= PackagingByCategoryVM.UnitPackagingByCategory.Id)
					{
						//change id 0 to existing Id
						PackagingByCategoryVM.UnitPackagingByCategory.Id = unitPackagingToUpdate.Id;
					}
					//this method is because  in previus version of the code i have problems with tracking and this method allows edit tracking elements
					_unitOfWork.UpdateEntityValues(unitPackagingToUpdate, PackagingByCategoryVM.UnitPackagingByCategory);
					TempData["success"] = _localizer["UnitPackagingUpdatedSuccessfully"].Value;
				}

				_unitOfWork.PackagingByCategory.Update(PackagingByCategoryVM.PackagingByCategory);
				_unitOfWork.Save();


				return RedirectToAction(nameof(Upsert), new { id = PackagingByCategoryVM.PackagingByCategory.CategoryId });
			}
			catch (Exception ex)
			{
				TempData["error"] = _localizer["UnexpectedError"].Value;
				_logger.LogError(ex, "Unexpected error while saving packaging assignment for category {CategoryId} and packaging {PackagingId}.", PackagingByCategoryVM.PackagingByCategory.CategoryId, PackagingByCategoryVM.UnitPackagingByCategory.PackagingId);
				return View(PackagingByCategoryVM);
			}


		}
		#region API CALLS

		[HttpGet]
		public IActionResult GetAllCategories()
		{
			List<Category> objCategoryList = _unitOfWork.Category.GetAll(u=> u.IsDeleted==false).ToList();
			var packagingTotalsByCategory = _pricingCalculatorService.GetPackagingTotalsByCategory();
			var categoryListDTOs = objCategoryList.Select(c => new CategoryDTO
			{
				Id = c.Id,
				Name = c.Name,
				TotalPackagingByCategory = packagingTotalsByCategory.GetValueOrDefault(c.Id)
			}).ToList();
			return Json(new { data = categoryListDTOs });
		}

		[HttpGet]
		public IActionResult GetAllUnitPackagings(int categoryId)
		{
			List<UnitPackagingByCategory> objUnitPackagingByCategories = _unitOfWork.UnitPackagingByCategory
				.GetAll(u => u.CategoryId == categoryId, includeProperties: "Packaging").ToList();

			var unitPackagingDTOs = objUnitPackagingByCategories.Select(unit => new
			{
				unit.Id,
				unit.Quantity,
				unit.Description,
				unit.CategoryId,
				unit.PackagingId,
				unit.Packaging,
				UnitTotal = unit.Packaging.Price / unit.Packaging.Quantity * unit.Quantity
			}).ToList();

			return Json(new { data = unitPackagingDTOs });
		}

		[HttpDelete]
		public IActionResult Delete(int? id)
		{
			UnitPackagingByCategory? unitToBeDeleted = _unitOfWork.UnitPackagingByCategory.Get(u => u.Id == id);
			if (unitToBeDeleted == null)
			{
				return Json(new { success = false, message = _localizer["ErrorWhileDeleting"].Value });
			}		
			_unitOfWork.UnitPackagingByCategory.Remove(unitToBeDeleted);
			_unitOfWork.Save();

			return Json(new { success = true, message = _localizer["DeleteSuccesfully"].Value });
		
		}

		[HttpGet]
		public IActionResult EditUnit(int unitId, int categoryId)
		{
			return RedirectToAction(nameof(Upsert), new { unitId = unitId, id = categoryId });
		}
	#endregion

	}
}





