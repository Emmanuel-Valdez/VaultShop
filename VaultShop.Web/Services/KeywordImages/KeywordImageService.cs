using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using SkiaSharp;
using VaultShop.Web.Services.ImageStorage;

namespace VaultShop.Web.Services.KeywordImages;

public sealed class KeywordImageService : IKeywordImageService
{
	private const long MaxFileSizeBytes = 10 * 1024 * 1024;
	private const int ChipSize = 400;
	private const int CoverWidth = 1400;
	private const int CoverHeight = 500;
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
		var validationError = ValidateFile(file);
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

		if (!CanDecodeImage(file))
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
		using var original = DecodeImageWithOrientation(inputStream);
		if (original is null)
		{
			throw new InvalidOperationException("Keyword image validation passed, but decoding failed while saving.");
		}

		await using var outputStream = new MemoryStream();
		WriteResizedJpeg(original, outputStream, targetWidth, targetHeight, squareCrop);

		return await _imageStorageService.SaveObjectAsync(new ImageStorageSaveRequest(
			$"keywords/keyword-{keywordId}",
			outputStream,
			file.FileName,
			"image/jpeg",
			outputStream.Length));
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

	// ponytail: decode/orient helpers duplicated from ProductImageService; extract to a shared Skia helper if a third image type appears.
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

	private static void WriteResizedJpeg(SKBitmap original, Stream outputStream, int targetWidth, int targetHeight, bool squareCrop)
	{
		// chip: center-crop to square, then scale. cover: fit inside target with white padding.
		if (squareCrop)
		{
			var crop = Math.Min(original.Width, original.Height);
			var cropX = (original.Width - crop) / 2;
			var cropY = (original.Height - crop) / 2;
			using var cropped = new SKBitmap(crop, crop);
			using (var canvas = new SKCanvas(cropped))
			{
				canvas.DrawBitmap(original,
					new SKRect(cropX, cropY, cropX + crop, cropY + crop),
					new SKRect(0, 0, crop, crop));
			}

			original = cropped;
		}

		using var resized = new SKBitmap(targetWidth, targetHeight);
		using (var canvas = new SKCanvas(resized))
		{
			canvas.Clear(SKColors.White);
			var scale = squareCrop
				? (float)targetWidth / original.Width
				: Math.Min((float)targetWidth / original.Width, (float)targetHeight / original.Height);
			var drawWidth = squareCrop ? targetWidth : (int)(original.Width * scale);
			var drawHeight = squareCrop ? targetHeight : (int)(original.Height * scale);
			var offsetX = (targetWidth - drawWidth) / 2;
			var offsetY = (targetHeight - drawHeight) / 2;
			canvas.DrawBitmap(original, new SKRect(offsetX, offsetY, offsetX + drawWidth, offsetY + drawHeight));
		}

		using var image = SKImage.FromBitmap(resized);
		using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
		data.SaveTo(outputStream);
	}
}

public sealed class KeywordImageValidationException : Exception
{
	public KeywordImageValidationException(string message) : base(message)
	{
	}
}