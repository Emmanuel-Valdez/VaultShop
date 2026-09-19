using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Globalization;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Models.CalculatorModels;
using VaultShop.Utility;
using VaultShop.Web.Services.CategoryImages;
using VaultShop.Web.Services.ImageStorage;

namespace VaultShop.Web.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
	public class CategoryController : Controller
	{
		public readonly IUnitOfWork _unitOfWork;
		private readonly IStringLocalizer<CategoryController> _localizer;
		private readonly ICategoryImageService _categoryImageService;
		private readonly IImageStorageService _imageStorageService;
		private readonly ILogger<CategoryController> _logger;

		public CategoryController(IUnitOfWork unitOfWork, IStringLocalizer<CategoryController> localizer, ICategoryImageService categoryImageService, IImageStorageService imageStorageService, ILogger<CategoryController> logger)
		{
			_unitOfWork = unitOfWork;
			_localizer = localizer;
			_categoryImageService = categoryImageService;
			_imageStorageService = imageStorageService;
			_logger = logger;
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
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Upsert(Category obj, IFormFile? imageFile)
		{
			if (obj.Name == obj.AvgShippingCost.ToString())
			{
				ModelState.AddModelError("name", _localizer["NameError"].Value);
			}
			if (obj.Name != null && obj.Name.ToLower() == "test")
			{
				ModelState.AddModelError("", _localizer["NameCantBeTest"].Value);
			}

			if (!ModelState.IsValid)
				return View(obj);

			var isNew = obj.Id == 0;

			// Validate before any persistence: a rejected upload leaves the category untouched (spec).
			if (imageFile is { Length: > 0 })
			{
				try
				{
					_categoryImageService.Validate(obj.Id, imageFile);
				}
				catch (CategoryImageValidationException ex)
				{
					ModelState.AddModelError("", ex.Message);
					var reloaded = isNew
						? null
						: _unitOfWork.Category.Get(u => u.Id == obj.Id && u.IsDeleted == false, includeProperties: "PackagingByCategory");
					return View(reloaded ?? obj);
				}
			}

			if (isNew)
			{
				if (imageFile is not { Length: > 0 })
				{
					// Only SaveAsync may set storage fields; a crafted POST without a file must not.
					obj.ImageUrl = null;
					obj.ObjectKey = null;
					obj.FileName = null;
					obj.ContentType = null;
					obj.SizeBytes = null;
					obj.StorageProvider = null;
				}
				obj.PackagingByCategory = new();
				_unitOfWork.Category.Add(obj);
				_unitOfWork.Save();
			}
			else
			{
				var existing = _unitOfWork.Category.Get(u => u.Id == obj.Id && u.IsDeleted == false);
				if (existing == null)
					return NotFound();
				// preserve image columns and packaging when form does not post them
				obj.ImageUrl = existing.ImageUrl;
				obj.ObjectKey = existing.ObjectKey;
				obj.FileName = existing.FileName;
				obj.ContentType = existing.ContentType;
				obj.SizeBytes = existing.SizeBytes;
				obj.StorageProvider = existing.StorageProvider;
				obj.PackagingByCategory = existing.PackagingByCategory;
				_unitOfWork.Category.Update(obj);
				_unitOfWork.Save();
			}

			try
			{
				if (imageFile is { Length: > 0 })
				{
					await ReplaceImageAsync(obj, imageFile);
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Category {CategoryId} image upload failed.", obj.Id);
				TempData["warning"] = _localizer["CategorySavedImageFailed"].Value;
			}

			TempData["success"] = isNew
				? _localizer["CategoryCreatedSuccess"].Value
				: _localizer["CategoryEditedSuccess"].Value;
			return RedirectToAction("Index");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteImage(int categoryId)
		{
			var category = _unitOfWork.Category.Get(u => u.Id == categoryId && u.IsDeleted == false);
			if (category == null)
				return NotFound();

			var objectKey = category.ObjectKey;
			var storageProvider = category.StorageProvider;

			category.ImageUrl = null;
			category.ObjectKey = null;
			category.FileName = null;
			category.ContentType = null;
			category.SizeBytes = null;
			category.StorageProvider = null;
			_unitOfWork.Category.Update(category);
			_unitOfWork.Save();

			if (!string.IsNullOrWhiteSpace(objectKey))
			{
				try
				{
					await _imageStorageService.DeleteObjectAsync(new DeleteObjectRequest(objectKey, storageProvider!, $"categories/category-{categoryId}"));
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Category {CategoryId} image was deleted from DB, but storage cleanup failed.", categoryId);
				}
			}

			TempData["success"] = _localizer["ImageDeletedSuccessfully"].Value;
			return RedirectToAction(nameof(Upsert), new { id = categoryId });
		}

		#region API CALLS
		[HttpGet]
		public IActionResult GetAll()
		{
			List<Category> objCategoryList = _unitOfWork.Category.GetAll(u => u.IsDeleted == false).ToList();
			return Json(new { data = objCategoryList });

		}
		[HttpPost]
		public async Task<IActionResult> Delete(int? id)
		{
			Category? categoryToBeDeleted = _unitOfWork.Category.Get(u => u.Id == id && u.IsDeleted == false);
			if (categoryToBeDeleted == null)
			{
				return Json(new { success = false, message = _localizer["ErrorWhileDeleting"].Value });
			}

			int activeProductCount = _unitOfWork.Product
				.GetAll(p => p.CategoryId == id && !p.IsDeleted)
				.Count();
			if (activeProductCount > 0)
			{
				return Json(new { success = false, message = _localizer["DeleteBlockedHasProducts", activeProductCount].Value });
			}

			var packaging = _unitOfWork.PackagingByCategory
				.Get(p => p.CategoryId == id, includeProperties: "UnitPackagingByCategoryList");
			if (packaging != null)
			{
				_unitOfWork.UnitPackagingByCategory.RemoveRange(packaging.UnitPackagingByCategoryList);
				_unitOfWork.PackagingByCategory.Remove(packaging);
			}

			var oldObjectKey = categoryToBeDeleted.ObjectKey;
			var oldStorageProvider = categoryToBeDeleted.StorageProvider;
			var hadImage = !string.IsNullOrWhiteSpace(oldObjectKey);

			if (hadImage)
			{
				categoryToBeDeleted.ImageUrl = null;
				categoryToBeDeleted.ObjectKey = null;
				categoryToBeDeleted.FileName = null;
				categoryToBeDeleted.ContentType = null;
				categoryToBeDeleted.SizeBytes = null;
				categoryToBeDeleted.StorageProvider = null;
			}

			categoryToBeDeleted.IsDeleted = true;
			_unitOfWork.Category.Update(categoryToBeDeleted);
			_unitOfWork.Save();

			if (hadImage)
			{
				try
				{
					await _imageStorageService.DeleteObjectAsync(new DeleteObjectRequest(oldObjectKey!, oldStorageProvider!, $"categories/category-{id}"));
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Category {CategoryId} was soft-deleted, but image storage cleanup failed.", id);
				}
			}

			return Json(new { success = true, message = _localizer["DeleteSuccesfully"].Value });
		}

		private async Task ReplaceImageAsync(Category category, IFormFile file)
		{
			// Save new image first — only delete old after new succeeds to prevent data loss.
			var stored = await _categoryImageService.SaveAsync(category.Id, file);

			var oldObjectKey = category.ObjectKey;
			var oldStorageProvider = category.StorageProvider;

			category.ImageUrl = stored.ImageUrl;
			category.ObjectKey = stored.ObjectKey;
			category.FileName = stored.FileName;
			category.ContentType = stored.ContentType;
			category.SizeBytes = stored.SizeBytes;
			category.StorageProvider = stored.StorageProvider;
			_unitOfWork.Save();

			// Best-effort cleanup of old storage after DB is consistent.
			if (!string.IsNullOrWhiteSpace(oldObjectKey))
			{
				try
				{
					await _imageStorageService.DeleteObjectAsync(new DeleteObjectRequest(oldObjectKey, oldStorageProvider!, $"categories/category-{category.Id}"));
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Category {CategoryId} image was replaced, but old storage cleanup failed.", category.Id);
				}
			}
		}


		#endregion
	}
}
