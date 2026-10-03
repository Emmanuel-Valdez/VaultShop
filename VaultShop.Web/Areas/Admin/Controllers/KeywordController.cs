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
			// ponytail: ValidateNever on Slug + clearing lets "Dejalo vacío…" work without the implicit NRT required error.
			ModelState.Remove(nameof(obj.Slug));
			var (slug, slugError) = SlugHelper.ResolveSlugOrDefault(obj.Slug, obj.Name);
			obj.Slug = slug;

			if (slugError != null)
			{
				ModelState.AddModelError(nameof(obj.Slug), _localizer[slugError].Value);
			}
			else if (_unitOfWork.Keyword.Get(u => u.IsDeleted == false && u.Id != obj.Id && u.Slug == obj.Slug) != null)
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
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Keyword {KeywordId} image upload failed.", obj.Id);
				TempData["warning"] = _localizer["KeywordSavedImageFailed"].Value;
			}

			TempData["success"] = isNew
				? _localizer["KeywordCreatedSuccess"].Value
				: _localizer["KeywordEditedSuccess"].Value;
			return RedirectToAction("Index");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteImage(int imageId)
		{
			var image = _unitOfWork.KeywordImage.Get(u => u.Id == imageId);
			if (image == null)
			{
				return NotFound();
			}

			int keywordId = image.KeywordId;

			// Cover deletes as a unit: all crop rows go together.
			List<KeywordImage> rows;
			if (image.Kind == KeywordImageKind.Chip)
			{
				rows = new List<KeywordImage> { image };
			}
			else
			{
				rows = _unitOfWork.KeywordImage.GetAll(i => i.KeywordId == keywordId && i.Kind != KeywordImageKind.Chip).ToList();
				if (!rows.Any(r => r.Id == image.Id))
				{
					rows.Add(image);
				}
			}

			_unitOfWork.KeywordImage.RemoveRange(rows);
			_unitOfWork.Save();

			foreach (var row in rows)
			{
				try
				{
					await _imageStorageService.DeleteObjectAsync(new DeleteObjectRequest(row.ObjectKey, row.StorageProvider, $"keywords/keyword-{keywordId}"));
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Keyword image row {ImageId} for keyword {KeywordId} was deleted, but storage cleanup failed.", row.Id, keywordId);
				}
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
			if (kind == KeywordImageKind.Chip)
			{
				// Save new image first — only delete old after new succeeds to prevent data loss.
				var stored = await _keywordImageService.SaveChipAsync(keywordId, file);

				var existing = _unitOfWork.KeywordImage.Get(i => i.KeywordId == keywordId && i.Kind == kind);
				if (existing != null)
				{
					_unitOfWork.KeywordImage.Remove(existing);
				}

				_unitOfWork.KeywordImage.Add(ToKeywordImage(keywordId, kind, stored));
				_unitOfWork.Save();

				// Best-effort cleanup of old storage after DB is consistent.
				if (existing != null)
				{
					try
					{
						await _imageStorageService.DeleteObjectAsync(new DeleteObjectRequest(existing.ObjectKey, existing.StorageProvider, $"keywords/keyword-{keywordId}"));
					}
					catch (Exception ex)
					{
						_logger.LogWarning(ex, "Keyword {KeywordId} {Kind} image row {ImageId} was replaced, but old storage cleanup failed.", keywordId, kind, existing.Id);
					}
				}
				return;
			}

		// Cover: save new objects first, then replace all cover rows, then best-effort delete each old object.
		// Full masters yield the 3-row set; smaller masters yield one Cover row with the widest
		// fitting variant (never upscaled) — the hero serves a lone Cover as a single img.
		var variants = await _keywordImageService.SaveCoverAsync(keywordId, file);

		var existingCovers = _unitOfWork.KeywordImage.GetAll(i => i.KeywordId == keywordId && i.Kind != KeywordImageKind.Chip).ToList();
		foreach (var old in existingCovers)
		{
			_unitOfWork.KeywordImage.Remove(old);
		}

		if (variants.Large is not null && variants.Medium is not null)
		{
			_unitOfWork.KeywordImage.Add(ToKeywordImage(keywordId, KeywordImageKind.Cover, variants.Large));
			_unitOfWork.KeywordImage.Add(ToKeywordImage(keywordId, KeywordImageKind.CoverMedium, variants.Medium));
			_unitOfWork.KeywordImage.Add(ToKeywordImage(keywordId, KeywordImageKind.CoverSmall, variants.Small));
		}
		else
		{
			// ponytail: partial master — one Cover row with the widest fitting variant.
			_unitOfWork.KeywordImage.Add(ToKeywordImage(keywordId, KeywordImageKind.Cover, variants.Large ?? variants.Medium ?? variants.Small));
		}
		_unitOfWork.Save();

			foreach (var old in existingCovers)
			{
				try
				{
					await _imageStorageService.DeleteObjectAsync(new DeleteObjectRequest(old.ObjectKey, old.StorageProvider, $"keywords/keyword-{keywordId}"));
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Keyword {KeywordId} cover image row {ImageId} was replaced, but old storage cleanup failed.", keywordId, old.Id);
				}
			}
		}

		private static KeywordImage ToKeywordImage(int keywordId, KeywordImageKind kind, StoredImage stored) => new() { KeywordId = keywordId, Kind = kind, ImageUrl = stored.ImageUrl, ObjectKey = stored.ObjectKey, FileName = stored.FileName, ContentType = stored.ContentType, SizeBytes = stored.SizeBytes, StorageProvider = stored.StorageProvider };
	}
}