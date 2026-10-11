using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Models.ViewModels;
using VaultShop.Utility;
using VaultShop.Web.Services.BannerImages;

namespace VaultShop.Web.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
	public class BannerController : Controller
	{
		public readonly IUnitOfWork _unitOfWork;
		private readonly IStringLocalizer<BannerController> _localizer;
		private readonly IBannerImageService _bannerImageService;
		private readonly ILogger<BannerController> _logger;

		public BannerController(IUnitOfWork unitOfWork, IStringLocalizer<BannerController> localizer, IBannerImageService bannerImageService, ILogger<BannerController> logger)
		{
			_unitOfWork = unitOfWork;
			_localizer = localizer;
			_bannerImageService = bannerImageService;
			_logger = logger;
		}

		public IActionResult Index()
		{
			return View();
		}

		public IActionResult Upsert(int? id)
		{
			BannerVM bannerVM = new();
			if (id == 0 || id == null)
			{
				PopulateFormData(bannerVM);
				return View(bannerVM);
			}

			Banner? bannerFromDb = _unitOfWork.Banner.Get(u => u.Id == id, includeProperties: "Images");
			if (bannerFromDb == null)
			{
				return NotFound();
			}

			bannerVM.Banner = bannerFromDb;
			PopulateFormData(bannerVM);
			return View(bannerVM);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Upsert(BannerVM bannerVM, IFormFile? coverFile, IFormFile? mobileFile)
		{
			var obj = bannerVM.Banner;
			var isNew = obj.Id == 0;

		// Store target carries no entity; a stale picker value must not persist.
		if (obj.TargetKind == BannerTargetKind.Store)
		{
			obj.TargetId = null;
		}

		// Safe zone is all-or-nothing: empty clears a prior rect; partial or
		// out-of-bounds is rejected before any persistence (spec).
		var safeZone = new decimal?[] { obj.SafeZoneLeft, obj.SafeZoneTop, obj.SafeZoneWidth, obj.SafeZoneHeight };
		var safeZoneSet = safeZone.Count(v => v.HasValue);
		if (safeZoneSet == 0)
		{
			obj.SafeZoneLeft = obj.SafeZoneTop = obj.SafeZoneWidth = obj.SafeZoneHeight = null;
		}
		else if (safeZoneSet < 4
			|| safeZone.Any(v => v!.Value < 0 || v!.Value > 1)
			|| obj.SafeZoneLeft!.Value + obj.SafeZoneWidth!.Value > 1
			|| obj.SafeZoneTop!.Value + obj.SafeZoneHeight!.Value > 1)
		{
			ModelState.AddModelError("", _localizer["SafeZoneInvalid"].Value);
		}

			// Validate before any persistence: a rejected upload leaves the banner untouched (spec).
			if (coverFile is { Length: > 0 })
			{
				try
				{
					_bannerImageService.ValidateCover(obj.Id, coverFile);
				}
				catch (BannerImageValidationException ex)
				{
					ModelState.AddModelError("", ex.Message);
				}
			}
			if (mobileFile is { Length: > 0 })
			{
				try
				{
					_bannerImageService.ValidateMobile(obj.Id, mobileFile);
				}
				catch (BannerImageValidationException ex)
				{
					ModelState.AddModelError("", ex.Message);
				}
			}
			if (isNew && coverFile is not { Length: > 0 })
			{
				ModelState.AddModelError("", _localizer["CoverImageRequired"].Value);
			}

			if (!ModelState.IsValid)
			{
				if (!isNew)
				{
					obj.Images = _unitOfWork.BannerImage.GetAll(i => i.BannerId == obj.Id).ToList();
				}
				PopulateFormData(bannerVM);
				return View(bannerVM);
			}

			if (isNew)
			{
				_unitOfWork.Banner.Add(obj);
				_unitOfWork.Save();
			}
			else
			{
				var existing = _unitOfWork.Banner.Get(u => u.Id == obj.Id, includeProperties: "Images");
				if (existing == null)
				{
					return NotFound();
				}
				// preserve image rows when the form does not post them
				obj.Images = existing.Images;
				_unitOfWork.Banner.Update(obj);
				_unitOfWork.Save();
			}

			try
			{
				if (coverFile is { Length: > 0 })
				{
					await _bannerImageService.ReplaceCoverAsync(obj.Id, coverFile);
				}
				if (mobileFile is { Length: > 0 })
				{
					await _bannerImageService.ReplaceMobileAsync(obj.Id, mobileFile);
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Banner {BannerId} image upload failed.", obj.Id);
				TempData["warning"] = _localizer["BannerSavedImageFailed"].Value;
			}

			TempData["success"] = isNew
				? _localizer["BannerCreatedSuccess"].Value
				: _localizer["BannerEditedSuccess"].Value;
			return RedirectToAction("Index");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteImage(int imageId)
		{
			var image = _unitOfWork.BannerImage.Get(u => u.Id == imageId);
			if (image == null)
			{
				return NotFound();
			}

			int bannerId = image.BannerId;

			// Cover deletes as a unit: all density rows go together.
			if (image.Kind == BannerImageKind.Mobile)
			{
				await _bannerImageService.DeleteImagesAsync(bannerId, BannerImageKind.Mobile);
			}
			else
			{
				await _bannerImageService.DeleteImagesAsync(bannerId, BannerImageKind.Master, BannerImageKind.Medium, BannerImageKind.Small);
			}

			TempData["success"] = _localizer["DeletedSuccessfully"].Value;
			return RedirectToAction(nameof(Upsert), new { id = bannerId });
		}

		#region API CALLS
		[HttpGet]
		public IActionResult GetAll()
		{
			var banners = _unitOfWork.Banner.GetAll(includeProperties: "Images").ToList();

			var data = banners.Select(b => new
			{
				b.Id,
				b.Title,
				layout = _localizer[$"Layout_{b.Layout}"].Value,
				b.SortOrder,
				b.IsActive,
				coverImage = b.Images.FirstOrDefault(i => i.Kind == BannerImageKind.Master)?.ImageUrl
					?? b.Images.FirstOrDefault(i => i.Kind == BannerImageKind.Medium)?.ImageUrl
					?? b.Images.FirstOrDefault(i => i.Kind == BannerImageKind.Small)?.ImageUrl,
				targetKind = _localizer[$"TargetKind_{b.TargetKind}"].Value,
				b.TargetId
			});
			return Json(new { data });
		}

		[HttpPost]
		public async Task<IActionResult> Delete(int? id)
		{
			Banner? bannerToBeDeleted = _unitOfWork.Banner.Get(u => u.Id == id);
			if (bannerToBeDeleted == null)
			{
				return Json(new { success = false, message = _localizer["ErrorWhileDeleting"].Value });
			}

			await _bannerImageService.DeleteAllAsync(bannerToBeDeleted.Id);

			_unitOfWork.Banner.Remove(bannerToBeDeleted);
			_unitOfWork.Save();
			return Json(new { success = true, message = _localizer["DeleteSuccesfully"].Value });
		}
		#endregion

		private void PopulateFormData(BannerVM bannerVM)
		{
			bannerVM.LayoutList = Enum.GetValues<BannerLayout>().Select(k => new SelectListItem
			{
				Text = _localizer[$"Layout_{k}"].Value,
				Value = ((int)k).ToString()
			});
			bannerVM.TextColorList = Enum.GetValues<BannerTextColor>().Select(k => new SelectListItem
			{
				Text = _localizer[$"TextColor_{k}"].Value,
				Value = ((int)k).ToString()
			});
			bannerVM.TargetKindList = Enum.GetValues<BannerTargetKind>().Select(k => new SelectListItem
			{
				Text = _localizer[$"TargetKind_{k}"].Value,
				Value = ((int)k).ToString()
			});
			var categoryGroup = new SelectListGroup { Name = _localizer["Group_Categories"].Value };
			var collectionGroup = new SelectListGroup { Name = _localizer["Group_Collections"].Value };
			var productGroup = new SelectListGroup { Name = _localizer["Group_Products"].Value };
			bannerVM.TargetOptions = _unitOfWork.Category.GetAll(u => u.IsDeleted == false)
					.Select(u => new SelectListItem { Text = u.Name, Value = u.Id.ToString(), Group = categoryGroup })
				.Concat(_unitOfWork.Keyword.GetAll(u => u.IsDeleted == false)
					.Select(u => new SelectListItem { Text = u.Name, Value = u.Id.ToString(), Group = collectionGroup }))
				.Concat(_unitOfWork.Product.GetAll(u => u.IsDeleted == false)
					.Select(u => new SelectListItem { Text = u.Name, Value = u.Id.ToString(), Group = productGroup }));
		}
	}
}
