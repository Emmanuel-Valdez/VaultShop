using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using VaultShop.Web.Services.ImageProcessing;
using VaultShop.Web.Services.ImageStorage;

namespace VaultShop.Web.Services.KeywordImages;

public sealed class KeywordImageService : IKeywordImageService
{
	private const int ChipSize = 400;
	private const int CoverWidth = 1400;
	private const int CoverHeight = 500;

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
		=> await SaveAsync(keywordId, file, ChipSize, ChipSize, squareCrop: true);

	public async Task<StoredImage> SaveCoverAsync(int keywordId, IFormFile file)
		=> await SaveAsync(keywordId, file, CoverWidth, CoverHeight, squareCrop: false);

	private async Task<StoredImage> SaveAsync(int keywordId, IFormFile file, int targetWidth, int targetHeight, bool squareCrop)
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

		await using var inputStream = file.OpenReadStream();
		using var original = SkiaImageProcessor.DecodeImageWithOrientation(inputStream);
		if (original is null)
		{
			throw new InvalidOperationException("Keyword image validation passed, but decoding failed while saving.");
		}

		await using var outputStream = new MemoryStream();
		SkiaImageProcessor.WriteResizedJpeg(original, outputStream, targetWidth, targetHeight, squareCrop);

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