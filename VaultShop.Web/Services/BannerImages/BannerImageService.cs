using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Web.Services.ImageProcessing;
using VaultShop.Web.Services.ImageStorage;

namespace VaultShop.Web.Services.BannerImages;

public sealed class BannerImageService : IBannerImageService
{
	// Landscape cover set at 2.67:1 (1905x714 master), same ratio as the keyword cover set.
	private const int CoverMasterWidth = 1905;
	private const int CoverMasterHeight = 714;
	private const int CoverMediumWidth = 1280;
	private const int CoverMediumHeight = 480;
	private const int CoverSmallWidth = 768;
	private const int CoverSmallHeight = 288;
	// Optional portrait asset at 4:5 for narrow viewports.
	private const int MobileWidth = 1200;
	private const int MobileHeight = 1500;
	// Relaxed gate: the optional asset may upscale modestly (<=2x), so a typical phone photo
	// (>=600x750) is accepted. The required cover pipeline above stays strict (no upscale).
	private const int MobileMinWidth = 600;
	private const int MobileMinHeight = 750;

	private static readonly BannerImageKind[] CoverKinds = [BannerImageKind.Master, BannerImageKind.Medium, BannerImageKind.Small];

	private readonly IUnitOfWork _unitOfWork;
	private readonly IImageStorageService _imageStorageService;
	private readonly ILogger<BannerImageService> _logger;
	private readonly IStringLocalizer<BannerImageService> _localizer;

	public BannerImageService(IUnitOfWork unitOfWork, IImageStorageService imageStorageService, ILogger<BannerImageService> logger, IStringLocalizer<BannerImageService> localizer)
	{
		_unitOfWork = unitOfWork;
		_imageStorageService = imageStorageService;
		_logger = logger;
		_localizer = localizer;
	}

	public async Task<BannerCoverVariants> SaveCoverAsync(int bannerId, IFormFile file)
	{
		ValidateUpload(bannerId, file);

		using var original = DecodeValidated(file);

		// ponytail: smallest variant gates — larger variants are generated only when the
		// master covers them, so the pipeline never upscales. Partial masters yield a
		// partial set; each variant maps 1:1 to its BannerImageKind row.
		if (!SkiaImageProcessor.CanDownscaleWithoutUpscale(original, CoverSmallWidth, CoverSmallHeight))
		{
			const string error = "UploadCoverTooSmall";
			_logger.LogWarning(
				"Rejected undersized banner cover upload for banner {BannerId}. FileName: {FileName}, Image: {Width}x{Height}",
				bannerId,
				file.FileName,
				original.Width,
				original.Height);
			throw new BannerImageValidationException(_localizer[error].Value);
		}

		var master = SkiaImageProcessor.CanDownscaleWithoutUpscale(original, CoverMasterWidth, CoverMasterHeight)
			? await SaveCropAsync(bannerId, file.FileName, original, CoverMasterWidth, CoverMasterHeight)
			: null;
		var medium = SkiaImageProcessor.CanDownscaleWithoutUpscale(original, CoverMediumWidth, CoverMediumHeight)
			? await SaveCropAsync(bannerId, file.FileName, original, CoverMediumWidth, CoverMediumHeight)
			: null;
		var small = await SaveCropAsync(bannerId, file.FileName, original, CoverSmallWidth, CoverSmallHeight);
		return new BannerCoverVariants(master, medium, small);
	}

	public async Task<StoredImage?> SaveMobileAsync(int bannerId, IFormFile file)
	{
		ValidateUpload(bannerId, file);

		using var original = DecodeValidated(file);

		// ponytail: mobile is optional — a relaxed minimum (half the target, <=2x upscale) keeps
		// typical phone photos usable; sub-minimum sources yield null, not an error.
		if (!SkiaImageProcessor.CanDownscaleWithoutUpscale(original, MobileMinWidth, MobileMinHeight))
		{
			_logger.LogInformation(
				"Skipped banner mobile asset for banner {BannerId}: source {Width}x{Height} cannot cover {TargetWidth}x{TargetHeight} without upscaling.",
				bannerId,
				original.Width,
				original.Height,
				MobileWidth,
				MobileHeight);
			return null;
		}

		return await SaveCropAsync(bannerId, file.FileName, original, MobileWidth, MobileHeight);
	}

	public void ValidateCover(int bannerId, IFormFile file)
	{
		ValidateUpload(bannerId, file);

		using var original = DecodeValidated(file);
		if (!SkiaImageProcessor.CanDownscaleWithoutUpscale(original, CoverSmallWidth, CoverSmallHeight))
		{
			throw new BannerImageValidationException(_localizer["UploadCoverTooSmall"].Value);
		}
	}

	public void ValidateMobile(int bannerId, IFormFile file)
	{
		ValidateUpload(bannerId, file);

		using var original = DecodeValidated(file);
		if (!SkiaImageProcessor.CanDownscaleWithoutUpscale(original, MobileMinWidth, MobileMinHeight))
		{
			throw new BannerImageValidationException(_localizer["UploadMobileTooSmall"].Value);
		}
	}

	public async Task ReplaceCoverAsync(int bannerId, IFormFile file)
	{
		// Save new objects first — only delete old after new succeeds to prevent data loss.
		var variants = await SaveCoverAsync(bannerId, file);

		// tracked: the caller (BannerController.Upsert) has the banner graph attached; fresh
		// instances would collide on Remove ("another instance with the same key is already tracked").
		var existing = _unitOfWork.BannerImage.GetAll(i => i.BannerId == bannerId && CoverKinds.Contains(i.Kind), tracked: true).ToList();
		foreach (var old in existing)
		{
			_unitOfWork.BannerImage.Remove(old);
		}

		AddVariant(bannerId, BannerImageKind.Master, variants.Master);
		AddVariant(bannerId, BannerImageKind.Medium, variants.Medium);
		AddVariant(bannerId, BannerImageKind.Small, variants.Small);
		_unitOfWork.Save();

		// Best-effort cleanup of old storage after DB is consistent.
		await DeleteKeysAsync(bannerId, existing);
	}

	public async Task ReplaceMobileAsync(int bannerId, IFormFile file)
	{
		var stored = await SaveMobileAsync(bannerId, file);
		if (stored is null)
		{
			const string error = "UploadMobileTooSmall";
			throw new BannerImageValidationException(_localizer[error].Value);
		}

		var existing = _unitOfWork.BannerImage.GetAll(i => i.BannerId == bannerId && i.Kind == BannerImageKind.Mobile, tracked: true).ToList();
		foreach (var old in existing)
		{
			_unitOfWork.BannerImage.Remove(old);
		}

		_unitOfWork.BannerImage.Add(ToBannerImage(bannerId, BannerImageKind.Mobile, stored));
		_unitOfWork.Save();

		await DeleteKeysAsync(bannerId, existing);
	}

	public async Task DeleteImagesAsync(int bannerId, params BannerImageKind[] kinds)
	{
		if (kinds.Length == 0)
		{
			return;
		}

		var rows = _unitOfWork.BannerImage.GetAll(i => i.BannerId == bannerId && kinds.Contains(i.Kind), tracked: true).ToList();
		await RemoveRowsAsync(bannerId, rows);
	}

	public async Task DeleteAllAsync(int bannerId)
	{
		var rows = _unitOfWork.BannerImage.GetAll(i => i.BannerId == bannerId, tracked: true).ToList();
		await RemoveRowsAsync(bannerId, rows);
	}

	private void AddVariant(int bannerId, BannerImageKind kind, StoredImage? stored)
	{
		if (stored is null)
		{
			return;
		}

		_unitOfWork.BannerImage.Add(ToBannerImage(bannerId, kind, stored));
	}

	private async Task RemoveRowsAsync(int bannerId, List<BannerImage> rows)
	{
		if (rows.Count == 0)
		{
			return;
		}

		_unitOfWork.BannerImage.RemoveRange(rows);
		_unitOfWork.Save();

		await DeleteKeysAsync(bannerId, rows);
	}

	private async Task DeleteKeysAsync(int bannerId, List<BannerImage> rows)
	{
		foreach (var row in rows)
		{
			try
			{
				await _imageStorageService.DeleteObjectAsync(new DeleteObjectRequest(row.ObjectKey, row.StorageProvider, Prefix(bannerId)));
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Banner {BannerId} image row {ImageId} was removed, but storage cleanup failed.", bannerId, row.Id);
			}
		}
	}

	private async Task<StoredImage> SaveCropAsync(int bannerId, string fileName, SkiaSharp.SKBitmap original, int targetWidth, int targetHeight)
	{
		await using var outputStream = new MemoryStream();
		SkiaImageProcessor.WriteCroppedJpeg(original, outputStream, targetWidth, targetHeight);

		return await _imageStorageService.SaveObjectAsync(new ImageStorageSaveRequest(
			Prefix(bannerId),
			outputStream,
			fileName,
			"image/jpeg",
			outputStream.Length));
	}

	private void ValidateUpload(int bannerId, IFormFile file)
	{
		var validationError = SkiaImageProcessor.ValidateFile(file);
		if (validationError is not null)
		{
			_logger.LogWarning(
				"Rejected banner image upload for banner {BannerId}. FileName: {FileName}, Length: {Length}, ContentType: {ContentType}, Reason: {Reason}",
				bannerId,
				file.FileName,
				file.Length,
				file.ContentType,
				validationError);
			throw new BannerImageValidationException(_localizer[validationError].Value);
		}

		if (!SkiaImageProcessor.CanDecodeImage(file))
		{
			const string error = "UploadFileInvalidImage";
			_logger.LogWarning(
				"Rejected undecodable banner image upload for banner {BannerId}. FileName: {FileName}, Length: {Length}, ContentType: {ContentType}",
				bannerId,
				file.FileName,
				file.Length,
				file.ContentType);
			throw new BannerImageValidationException(_localizer[error].Value);
		}
	}

	private static string Prefix(int bannerId) => $"banners/banner-{bannerId}";

	private static SkiaSharp.SKBitmap DecodeValidated(IFormFile file)
	{
		using var stream = file.OpenReadStream();
		return SkiaImageProcessor.DecodeImageWithOrientation(stream)
			?? throw new InvalidOperationException("Banner image validation passed, but decoding failed while saving.");
	}

	private static BannerImage ToBannerImage(int bannerId, BannerImageKind kind, StoredImage stored) => new() { BannerId = bannerId, Kind = kind, ImageUrl = stored.ImageUrl, ObjectKey = stored.ObjectKey, FileName = stored.FileName, ContentType = stored.ContentType, SizeBytes = stored.SizeBytes, StorageProvider = stored.StorageProvider };
}

public sealed class BannerImageValidationException : Exception
{
	public BannerImageValidationException(string message) : base(message)
	{
	}
}

public sealed record BannerCoverVariants(StoredImage? Master, StoredImage? Medium, StoredImage Small);
