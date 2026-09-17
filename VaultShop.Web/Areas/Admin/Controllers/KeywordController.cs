using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Utility;
using VaultShop.Web.Services.ImageStorage;
using VaultShop.Web.Services.KeywordImages;

namespace VaultShop.Web.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
	public class KeywordController : Controller
	{
		public readonly IUnitOfWork _unitOfWork;
		private readonly IStringLocalizer<KeywordController> _localizer;
		private readonly IKeywordImageService _keywordImageService;
		private readonly IImageStorageService _imageStorageService;
		private readonly ILogger<KeywordController> _logger;

		public KeywordController(IUnitOfWork unitOfWork, IStringLocalizer<KeywordController> localizer, IKeywordImageService keywordImageService, IImageStorageService imageStorageService, ILogger<KeywordController> logger)
		{
			_unitOfWork = unitOfWork;
			_localizer = localizer;
			_keywordImageService = keywordImageService;
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
				return View(new Keyword());
			}

			Keyword? keyword = _unitOfWork.Keyword.Get(u => u.Id == id && u.IsDeleted == false, includeProperties: "Images");
			if (keyword == null)
			{
				return NotFound();
			}

			return View(keyword);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Upsert(Keyword obj, IFormFile? chipFile, IFormFile? coverFile)
		{
			if (string.IsNullOrWhiteSpace(obj.Slug))
			{
				obj.Slug = SlugHelper.Slugify(obj.Name);
			}
			obj.Slug = obj.Slug.Trim();

			var existingWithSlug = _unitOfWork.Keyword.Get(u => u.IsDeleted == false && u.Id != obj.Id && u.Slug == obj.Slug);
			if (existingWithSlug != null)
			{
				ModelState.AddModelError(nameof(obj.Slug), _localizer["SlugAlreadyExists"].Value);
			}

			if (!ModelState.IsValid)
			{
				return View(obj);
			}

			var isNew = obj.Id == 0;
			if (isNew)
			{
				_unitOfWork.Keyword.Add(obj);
			}
			else
			{
				_unitOfWork.Keyword.Update(obj);
			}
			_unitOfWork.Save();

			try
			{
				if (chipFile is { Length: > 0 })
				{
					await ReplaceImageAsync(obj.Id, KeywordImageKind.Chip, chipFile);
				}
				if (coverFile is { Length: > 0 })
				{
					await ReplaceImageAsync(obj.Id, KeywordImageKind.Cover, coverFile);
				}
			}
			catch (KeywordImageValidationException ex)
			{
				ModelState.Clear();
				ModelState.AddModelError("", ex.Message);

				var reloaded = _unitOfWork.Keyword.Get(u => u.Id == obj.Id && u.IsDeleted == false, includeProperties: "Images");
				return View(reloaded ?? obj);
			}

			TempData["success"] = isNew
				? _localizer["KeywordCreatedSuccess"].Value
				: _localizer["KeywordEditedSuccess"].Value;
			return RedirectToAction("Index");
		}

		public async Task<IActionResult> DeleteImage(int imageId)
		{
			var image = _unitOfWork.KeywordImage.Get(u => u.Id == imageId);
			if (image == null)
			{
				return NotFound();
			}

			int keywordId = image.KeywordId;
			_unitOfWork.KeywordImage.Remove(image);
			_unitOfWork.Save();

			try
			{
				await _imageStorageService.DeleteObjectAsync(new DeleteObjectRequest(image.ObjectKey, image.StorageProvider, $"keywords/keyword-{keywordId}"));
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Keyword image row {ImageId} for keyword {KeywordId} was deleted, but storage cleanup failed.", imageId, keywordId);
			}

			TempData["success"] = _localizer["DeletedSuccessfully"].Value;
			return RedirectToAction(nameof(Upsert), new { id = keywordId });
		}

		#region API CALLS
		[HttpGet]
		public IActionResult GetAll()
		{
			List<Keyword> keywords = _unitOfWork.Keyword
				.GetAll(u => u.IsDeleted == false, includeProperties: "Images,ProductKeywords.Product")
				.ToList();

			var data = keywords.Select(k => new
			{
				k.Id,
				k.Name,
				k.Slug,
				chipImage = k.Images.FirstOrDefault(i => i.Kind == KeywordImageKind.Chip)?.ImageUrl,
				productCount = k.ProductKeywords.Count(pk => pk.Product != null && !pk.Product.IsDeleted)
			});
			return Json(new { data });
		}

		[HttpPost]
		public async Task<IActionResult> Delete(int? id)
		{
			Keyword? keywordToBeDeleted = _unitOfWork.Keyword.Get(u => u.Id == id && u.IsDeleted == false);
			if (keywordToBeDeleted == null)
			{
				return Json(new { success = false, message = _localizer["ErrorWhileDeleting"].Value });
			}

			int activeProductCount = _unitOfWork.ProductKeyword
				.GetAll(pk => pk.KeywordId == id, includeProperties: "Product")
				.Count(pk => !pk.Product.IsDeleted);
			if (activeProductCount > 0)
			{
				return Json(new { success = false, message = _localizer["DeleteBlockedHasProducts", activeProductCount].Value });
			}

			var links = _unitOfWork.ProductKeyword.GetAll(pk => pk.KeywordId == id).ToList();
			if (links.Count > 0)
			{
				_unitOfWork.ProductKeyword.RemoveRange(links);
			}

			var images = _unitOfWork.KeywordImage.GetAll(i => i.KeywordId == id).ToList();
			if (images.Count > 0)
			{
				_unitOfWork.KeywordImage.RemoveRange(images);
				foreach (var image in images)
				{
					try
					{
						await _imageStorageService.DeleteObjectAsync(new DeleteObjectRequest(image.ObjectKey, image.StorageProvider, $"keywords/keyword-{id}"));
					}
					catch (Exception ex)
					{
						_logger.LogWarning(ex, "Keyword {KeywordId} image row {ImageId} was removed, but storage cleanup failed.", id, image.Id);
					}
				}
			}

			keywordToBeDeleted.IsDeleted = true;
			_unitOfWork.Keyword.Update(keywordToBeDeleted);
			_unitOfWork.Save();
			return Json(new { success = true, message = _localizer["DeleteSuccesfully"].Value });
		}
		#endregion

		private async Task ReplaceImageAsync(int keywordId, KeywordImageKind kind, IFormFile file)
		{
			var existing = _unitOfWork.KeywordImage.Get(i => i.KeywordId == keywordId && i.Kind == kind);
			if (existing != null)
			{
				_unitOfWork.KeywordImage.Remove(existing);
				_unitOfWork.Save();

				try
				{
					await _imageStorageService.DeleteObjectAsync(new DeleteObjectRequest(existing.ObjectKey, existing.StorageProvider, $"keywords/keyword-{keywordId}"));
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Keyword {KeywordId} {Kind} image row {ImageId} was replaced, but old storage cleanup failed.", keywordId, kind, existing.Id);
				}
			}

			var stored = kind == KeywordImageKind.Chip
				? await _keywordImageService.SaveChipAsync(keywordId, file)
				: await _keywordImageService.SaveCoverAsync(keywordId, file);

			_unitOfWork.KeywordImage.Add(new KeywordImage
			{
				KeywordId = keywordId,
				Kind = kind,
				ImageUrl = stored.ImageUrl,
				ObjectKey = stored.ObjectKey,
				FileName = stored.FileName,
				ContentType = stored.ContentType,
				SizeBytes = stored.SizeBytes,
				StorageProvider = stored.StorageProvider
			});
			_unitOfWork.Save();
		}
	}
}