using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using SkiaSharp;
using UkiyoDesignsWeb.Services.ImageStorage;

namespace UkiyoDesignsWeb.Services.ProductImages;

public sealed class ProductImageService : IProductImageService
{
	private const long MaxFileSizeBytes = 10 * 1024 * 1024;
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
				using var original = DecodeImageWithOrientation(inputStream);
				if (original is null)
				{
					throw new InvalidOperationException("Image validation passed, but decoding failed while saving.");
				}

				await using var outputStream = new MemoryStream();
				WriteResizedJpeg(original, outputStream);

				var storedImage = await _imageStorageService.SaveProductImageAsync(new ImageStorageSaveRequest(
					productId,
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

	private static SKBitmap? DecodeImageWithOrientation(Stream stream)
	{
		using var codec = SKCodec.Create(stream);
		if (codec is null)
		{
			return null;
		}

		var decoded = SKBitmap.Decode(codec);
		if (decoded is null)
		{
			return null;
		}

		var oriented = ApplyEncodedOrigin(decoded, codec.EncodedOrigin);
		if (!ReferenceEquals(oriented, decoded))
		{
			decoded.Dispose();
		}

		return oriented;
	}

	private static SKBitmap ApplyEncodedOrigin(SKBitmap source, SKEncodedOrigin origin)
	{
		if (origin is SKEncodedOrigin.Default or SKEncodedOrigin.TopLeft)
		{
			return source;
		}

		var swapsDimensions = origin is SKEncodedOrigin.LeftTop
				or SKEncodedOrigin.RightTop
				or SKEncodedOrigin.RightBottom
				or SKEncodedOrigin.LeftBottom;

		var outputWidth = swapsDimensions ? source.Height : source.Width;
		var outputHeight = swapsDimensions ? source.Width : source.Height;
		var destination = new SKBitmap(outputWidth, outputHeight, source.ColorType, source.AlphaType);

		using var canvas = new SKCanvas(destination);
		switch (origin)
		{
			case SKEncodedOrigin.TopRight:
				canvas.Translate(source.Width, 0);
				canvas.Scale(-1, 1);
				break;
			case SKEncodedOrigin.BottomRight:
				canvas.Translate(source.Width, source.Height);
				canvas.RotateDegrees(180);
				break;
			case SKEncodedOrigin.BottomLeft:
				canvas.Translate(0, source.Height);
				canvas.Scale(1, -1);
				break;
			case SKEncodedOrigin.LeftTop:
				canvas.Scale(1, -1);
				canvas.RotateDegrees(90);
				break;
			case SKEncodedOrigin.RightTop:
				canvas.Translate(source.Height, 0);
				canvas.RotateDegrees(90);
				break;
			case SKEncodedOrigin.RightBottom:
				canvas.Translate(source.Height, source.Width);
				canvas.Scale(1, -1);
				canvas.RotateDegrees(270);
				break;
			case SKEncodedOrigin.LeftBottom:
				canvas.Translate(0, source.Width);
				canvas.RotateDegrees(270);
				break;
		}

		canvas.DrawBitmap(source, 0, 0);
		return destination;
	}

	private static void WriteResizedJpeg(SKBitmap original, Stream outputStream)
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
		data.SaveTo(outputStream);
	}
}
