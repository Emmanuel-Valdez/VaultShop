using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using SkiaSharp;

namespace UkiyoDesignsWeb.Services.ProductImages;

public sealed class ProductImageService : IProductImageService
{
	private const long MaxFileSizeBytes = 5 * 1024 * 1024;
	private const int OutputWidth = 1000;
	private const int OutputHeight = 1200;
	private const int JpegQuality = 75;

	private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".jpg",
		".jpeg",
		".png",
		".webp"
	};

	private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
	{
		"image/jpeg",
		"image/png",
		"image/webp"
	};

	private readonly IWebHostEnvironment _webHostEnvironment;
	private readonly ILogger<ProductImageService> _logger;
	private readonly IStringLocalizer<ProductImageService> _localizer;

	public ProductImageService(IWebHostEnvironment webHostEnvironment, ILogger<ProductImageService> logger, IStringLocalizer<ProductImageService> localizer)
	{
		_webHostEnvironment = webHostEnvironment;
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
			var validationError = ValidateFile(file);
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

			if (!CanDecodeImage(file))
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
				using var original = SKBitmap.Decode(inputStream);
				if (original is null)
				{
					throw new InvalidOperationException("Image validation passed, but decoding failed while saving.");
				}

				var productPath = Path.Combine("images", "products", $"product-{productId}");
				var finalPath = Path.Combine(_webHostEnvironment.WebRootPath, productPath);
				Directory.CreateDirectory(finalPath);

				var fileName = $"{Guid.NewGuid():N}.jpg";
				var outputFilePath = Path.Combine(finalPath, fileName);

				SaveResizedJpeg(original, outputFilePath);

				var imageUrl = "\\" + Path.Combine(productPath, fileName).Replace(Path.DirectorySeparatorChar, '\\');
				result.AddSavedImageUrl(imageUrl);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to save product image upload batch for product {ProductId}.", productId);
			throw;
		}

		return result;
	}

	private static string? ValidateFile(IFormFile file)
	{
		if (file.Length <= 0)
		{
			return "UploadFileEmpty";
		}

		if (file.Length > MaxFileSizeBytes)
		{
			return "UploadFileTooLarge";
		}

		var extension = Path.GetExtension(file.FileName);
		if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
		{
			return "UploadFileInvalidExtension";
		}

		if (string.IsNullOrWhiteSpace(file.ContentType) || !AllowedContentTypes.Contains(file.ContentType))
		{
			return "UploadFileInvalidContentType";
		}

		return null;
	}

	private static bool CanDecodeImage(IFormFile file)
	{
		using var stream = file.OpenReadStream();
		using var bitmap = SKBitmap.Decode(stream);
		return bitmap is not null;
	}

	private static void SaveResizedJpeg(SKBitmap original, string outputFilePath)
	{
		var aspectRatio = (float)original.Width / original.Height;
		var newWidth = original.Width > original.Height ? OutputWidth : (int)(OutputHeight * aspectRatio);
		var newHeight = original.Width > original.Height ? (int)(OutputWidth / aspectRatio) : OutputHeight;

		using var resizedBitmap = new SKBitmap(newWidth, newHeight);
		using (var canvas = new SKCanvas(resizedBitmap))
		{
			canvas.DrawBitmap(original, new SKRect(0, 0, newWidth, newHeight));
		}

		using var finalBitmap = new SKBitmap(OutputWidth, OutputHeight);
		using (var canvas = new SKCanvas(finalBitmap))
		{
			canvas.Clear(SKColors.White);
			var offsetX = (OutputWidth - newWidth) / 2;
			var offsetY = (OutputHeight - newHeight) / 2;
			canvas.DrawBitmap(resizedBitmap, offsetX, offsetY);
		}

		using var image = SKImage.FromBitmap(finalBitmap);
		using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
		using var outputStream = new FileStream(outputFilePath, FileMode.CreateNew, FileAccess.Write);
		data.SaveTo(outputStream);
	}
}
