using Microsoft.AspNetCore.Http;
using SkiaSharp;

namespace VaultShop.Web.Services.ImageProcessing;

public static class SkiaImageProcessor
{
	private const long MaxFileSizeBytes = 10 * 1024 * 1024;
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

	public static string? ValidateFile(IFormFile file)
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

	public static bool CanDecodeImage(IFormFile file)
	{
		using var stream = file.OpenReadStream();
		using var bitmap = SKBitmap.Decode(stream);
		return bitmap is not null;
	}

	public static SKBitmap? DecodeImageWithOrientation(Stream stream)
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

	public static void WriteCroppedJpeg(SKBitmap original, Stream outputStream, int targetWidth, int targetHeight)
	{
		using var resized = new SKBitmap(targetWidth, targetHeight);
		using (var canvas = new SKCanvas(resized))
		{
			DrawCenterCrop(original, canvas, targetWidth, targetHeight);
		}

		using var image = SKImage.FromBitmap(resized);
		using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
		data.SaveTo(outputStream);
	}

	private static void DrawCenterCrop(SKBitmap original, SKCanvas canvas, int targetWidth, int targetHeight)
	{
		var targetAspect = (float)targetWidth / targetHeight;
		var sourceAspect = (float)original.Width / original.Height;
		SKRect source;
		if (sourceAspect > targetAspect)
		{
			var cropWidth = original.Height * targetAspect;
			var cropX = (original.Width - cropWidth) / 2;
			source = new SKRect(cropX, 0, cropX + cropWidth, original.Height);
		}
		else
		{
			var cropHeight = original.Width / targetAspect;
			var cropY = (original.Height - cropHeight) / 2;
			source = new SKRect(0, cropY, original.Width, cropY + cropHeight);
		}
		canvas.DrawBitmap(original, source, new SKRect(0, 0, targetWidth, targetHeight));
	}

	public static void WriteResizedJpeg(SKBitmap original, Stream outputStream, int targetWidth, int targetHeight, bool squareCrop)
	{
		if (squareCrop)
		{
			WriteCroppedJpeg(original, outputStream, targetWidth, targetHeight);
			return;
		}

		var source = new SKRect(0, 0, original.Width, original.Height);
		var destination = new SKRect(0, 0, targetWidth, targetHeight);
		var scale = Math.Min((float)targetWidth / original.Width, (float)targetHeight / original.Height);
		var drawWidth = (int)(original.Width * scale);
		var drawHeight = (int)(original.Height * scale);
		var offsetX = (targetWidth - drawWidth) / 2;
		var offsetY = (targetHeight - drawHeight) / 2;
		destination = new SKRect(offsetX, offsetY, offsetX + drawWidth, offsetY + drawHeight);

		using var resized = new SKBitmap(targetWidth, targetHeight);
		using (var canvas = new SKCanvas(resized))
		{
			canvas.Clear(SKColors.White);
			canvas.DrawBitmap(original, source, destination);
		}

		using var image = SKImage.FromBitmap(resized);
		using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
		data.SaveTo(outputStream);
	}
}
