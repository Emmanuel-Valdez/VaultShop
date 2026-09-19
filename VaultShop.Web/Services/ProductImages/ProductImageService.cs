using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using VaultShop.Web.Services.ImageProcessing;
using VaultShop.Web.Services.ImageStorage;

namespace VaultShop.Web.Services.ProductImages;

public sealed class ProductImageService : IProductImageService
{
	private const int OutputWidth = 1000;
	private const int OutputHeight = 1200;

	private readonly IImageStorageService _imageStorageService;
	private readonly ILogger<ProductImageService> _logger;
	private readonly IStringLocalizer<ProductImageService> _localizer;

	public ProductImageService(IImageStorageService imageStorageService, ILogger<ProductImageService> logger, IStringLocalizer<ProductImageService> localizer)
	{
		_imageStorageService = imageStorageService;
		_logger = logger;
		_localizer = localizer;
	}

	public async Task<ProductImageUploadResult> SaveProductImagesAsync(int productId, IReadOnlyCollection<IFormFile>? files)
	{
		var result = new ProductImageUploadResult();

		if (files is null || files.Count == 0)
		{
			return result;
		}

		foreach (var file in files)
		{
			var validationError = SkiaImageProcessor.ValidateFile(file);
			if (validationError is not null)
			{
				_logger.LogWarning(
					"Rejected product image upload for product {ProductId}. FileName: {FileName}, Length: {Length}, ContentType: {ContentType}, Reason: {Reason}",
					productId,
					file.FileName,
					file.Length,
					file.ContentType,
					validationError);

				result.AddError(_localizer[validationError].Value);
				continue;
			}

			if (!SkiaImageProcessor.CanDecodeImage(file))
			{
				const string error = "UploadFileInvalidImage";
				_logger.LogWarning(
					"Rejected undecodable product image upload for product {ProductId}. FileName: {FileName}, Length: {Length}, ContentType: {ContentType}",
					productId,
					file.FileName,
					file.Length,
					file.ContentType);

				result.AddError(_localizer[error].Value);
			}
		}

		if (result.HasErrors)
		{
			return result;
		}

		try
		{
			foreach (var file in files)
			{
				await using var inputStream = file.OpenReadStream();
				using var original = SkiaImageProcessor.DecodeImageWithOrientation(inputStream);
				if (original is null)
				{
					throw new InvalidOperationException("Image validation passed, but decoding failed while saving.");
				}

				await using var outputStream = new MemoryStream();
				SkiaImageProcessor.WriteResizedJpeg(original, outputStream, OutputWidth, OutputHeight, squareCrop: false);

				var storedImage = await _imageStorageService.SaveObjectAsync(new ImageStorageSaveRequest(
					$"products/product-{productId}",
					outputStream,
					file.FileName,
					"image/jpeg",
					outputStream.Length));

				result.AddSavedImage(storedImage);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to save product image upload batch for product {ProductId}.", productId);
			throw;
		}

		return result;
	}
}
