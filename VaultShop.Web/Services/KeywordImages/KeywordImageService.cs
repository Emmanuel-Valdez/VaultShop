using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using VaultShop.Web.Services.ImageProcessing;
using VaultShop.Web.Services.ImageStorage;

namespace VaultShop.Web.Services.KeywordImages;

public sealed class KeywordImageService : IKeywordImageService
{
	private const int ChipSize = 400;
	// Band crops; comment names the --hero-h value each band assumes (site.css).
	private const int CoverLargeWidth = 1600;
	private const int CoverLargeHeight = 700; // --hero-h: 560
	private const int CoverMediumWidth = 1200;
	private const int CoverMediumHeight = 500; // --hero-h: 320
	private const int CoverSmallWidth = 780;
	private const int CoverSmallHeight = 520; // --hero-h: 260

	private readonly IImageStorageService _imageStorageService;
	private readonly ILogger<KeywordImageService> _logger;
	private readonly IStringLocalizer<KeywordImageService> _localizer;

	public KeywordImageService(IImageStorageService imageStorageService, ILogger<KeywordImageService> logger, IStringLocalizer<KeywordImageService> localizer)
	{
		_imageStorageService = imageStorageService;
		_logger = logger;
		_localizer = localizer;
	}

	public async Task<StoredImage> SaveChipAsync(int keywordId, IFormFile file)
		=> await SaveAsync(keywordId, file, ChipSize, ChipSize);

	public async Task<CoverVariants> SaveCoverAsync(int keywordId, IFormFile file)
	{
		ValidateUpload(keywordId, file);

		await using var inputStream = file.OpenReadStream();
		using var original = SkiaImageProcessor.DecodeImageWithOrientation(inputStream);
		if (original is null)
		{
			throw new InvalidOperationException("Keyword image validation passed, but decoding failed while saving.");
		}

		var large = await SaveCropAsync(keywordId, file.FileName, original, CoverLargeWidth, CoverLargeHeight);
		var medium = await SaveCropAsync(keywordId, file.FileName, original, CoverMediumWidth, CoverMediumHeight);
		var small = await SaveCropAsync(keywordId, file.FileName, original, CoverSmallWidth, CoverSmallHeight);
		return new CoverVariants(large, medium, small);
	}

	private async Task<StoredImage> SaveCropAsync(int keywordId, string fileName, SkiaSharp.SKBitmap original, int targetWidth, int targetHeight)
	{
		await using var outputStream = new MemoryStream();
		SkiaImageProcessor.WriteCroppedJpeg(original, outputStream, targetWidth, targetHeight);

		return await _imageStorageService.SaveObjectAsync(new ImageStorageSaveRequest(
			$"keywords/keyword-{keywordId}",
			outputStream,
			fileName,
			"image/jpeg",
			outputStream.Length));
	}

	private void ValidateUpload(int keywordId, IFormFile file)
	{
		var validationError = SkiaImageProcessor.ValidateFile(file);
		if (validationError is not null)
		{
			_logger.LogWarning(
				"Rejected keyword image upload for keyword {KeywordId}. FileName: {FileName}, Length: {Length}, ContentType: {ContentType}, Reason: {Reason}",
				keywordId,
				file.FileName,
				file.Length,
				file.ContentType,
				validationError);
			throw new KeywordImageValidationException(_localizer[validationError].Value);
		}

		if (!SkiaImageProcessor.CanDecodeImage(file))
		{
			const string error = "UploadFileInvalidImage";
			_logger.LogWarning(
				"Rejected undecodable keyword image upload for keyword {KeywordId}. FileName: {FileName}, Length: {Length}, ContentType: {ContentType}",
				keywordId,
				file.FileName,
				file.Length,
				file.ContentType);
			throw new KeywordImageValidationException(_localizer[error].Value);
		}
	}

	private async Task<StoredImage> SaveAsync(int keywordId, IFormFile file, int targetWidth, int targetHeight)
	{
		ValidateUpload(keywordId, file);

		await using var inputStream = file.OpenReadStream();
		using var original = SkiaImageProcessor.DecodeImageWithOrientation(inputStream);
		if (original is null)
		{
			throw new InvalidOperationException("Keyword image validation passed, but decoding failed while saving.");
		}

		await using var outputStream = new MemoryStream();
		// ponytail: SaveChipAsync siempre recorta cuadrado; sin param hasta que otro caller pida distinto.
		SkiaImageProcessor.WriteResizedJpeg(original, outputStream, targetWidth, targetHeight, true);

		return await _imageStorageService.SaveObjectAsync(new ImageStorageSaveRequest(
			$"keywords/keyword-{keywordId}",
			outputStream,
			file.FileName,
			"image/jpeg",
			outputStream.Length));
	}
}

public sealed class KeywordImageValidationException : Exception
{
	public KeywordImageValidationException(string message) : base(message)
	{
	}
}

public sealed record CoverVariants(StoredImage Large, StoredImage Medium, StoredImage Small);