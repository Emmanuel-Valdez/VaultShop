using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using VaultShop.Web.Services.ImageProcessing;
using VaultShop.Web.Services.ImageStorage;

namespace VaultShop.Web.Services.CategoryImages;

public sealed class CategoryImageService : ICategoryImageService
{
	private const int ChipSize = 400;

	private readonly IImageStorageService _imageStorageService;
	private readonly ILogger<CategoryImageService> _logger;
	private readonly IStringLocalizer<CategoryImageService> _localizer;

	public CategoryImageService(IImageStorageService imageStorageService, ILogger<CategoryImageService> logger, IStringLocalizer<CategoryImageService> localizer)
	{
		_imageStorageService = imageStorageService;
		_logger = logger;
		_localizer = localizer;
	}

	public void Validate(int categoryId, IFormFile file)
	{
		var validationError = SkiaImageProcessor.ValidateFile(file);
		if (validationError is not null)
		{
			_logger.LogWarning(
				"Rejected category image upload for category {CategoryId}. FileName: {FileName}, Length: {Length}, ContentType: {ContentType}, Reason: {Reason}",
				categoryId,
				file.FileName,
				file.Length,
				file.ContentType,
				validationError);
			throw new CategoryImageValidationException(_localizer[validationError].Value);
		}

		if (!SkiaImageProcessor.CanDecodeImage(file))
		{
			const string error = "UploadFileInvalidImage";
			_logger.LogWarning(
				"Rejected undecodable category image upload for category {CategoryId}. FileName: {FileName}, Length: {Length}, ContentType: {ContentType}",
				categoryId,
				file.FileName,
				file.Length,
				file.ContentType);
			throw new CategoryImageValidationException(_localizer[error].Value);
		}
	}

	public async Task<StoredImage> SaveAsync(int categoryId, IFormFile file)
	{
		Validate(categoryId, file);

		await using var inputStream = file.OpenReadStream();
		using var original = SkiaImageProcessor.DecodeImageWithOrientation(inputStream);
		if (original is null)
		{
			throw new InvalidOperationException("Category image validation passed, but decoding failed while saving.");
		}

		await using var outputStream = new MemoryStream();
		SkiaImageProcessor.WriteResizedJpeg(original, outputStream, ChipSize, ChipSize, squareCrop: true);

		return await _imageStorageService.SaveObjectAsync(new ImageStorageSaveRequest(
			$"categories/category-{categoryId}",
			outputStream,
			file.FileName,
			"image/jpeg",
			outputStream.Length));
	}
}

public sealed class CategoryImageValidationException : Exception
{
	public CategoryImageValidationException(string message) : base(message)
	{
	}
}
