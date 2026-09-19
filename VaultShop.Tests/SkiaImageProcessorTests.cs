using Microsoft.AspNetCore.Http;
using SkiaSharp;
using VaultShop.Web.Services.ImageProcessing;

namespace VaultShop.Web.Tests;

public class SkiaImageProcessorTests
{
	[Fact]
	public void ValidateFile_ExactlyTenMb_IsAccepted()
	{
		var file = CreateFormFile(new byte[1], 10 * 1024 * 1024, "cat.jpg", "image/jpeg");

		Assert.Null(SkiaImageProcessor.ValidateFile(file));
	}

	[Fact]
	public void ValidateFile_OverTenMb_ReturnsTooLarge()
	{
		var file = CreateFormFile(new byte[1], 10 * 1024 * 1024 + 1, "cat.jpg", "image/jpeg");

		Assert.Equal("UploadFileTooLarge", SkiaImageProcessor.ValidateFile(file));
	}

	[Fact]
	public void WriteResizedJpeg_Landscape_SquareCrop_CropsToSquare()
	{
		using var source = new SKBitmap(600, 400);
		source.Erase(SKColors.Blue);

		using var output = Resize(source, squareCrop: true, targetWidth: 400, targetHeight: 400);
		Assert.Equal(400, output.Width);
		Assert.Equal(400, output.Height);
	}

	[Fact]
	public void WriteResizedJpeg_Portrait_SquareCrop_CropsToSquare()
	{
		using var source = new SKBitmap(400, 600);
		source.Erase(SKColors.Green);

		using var output = Resize(source, squareCrop: true, targetWidth: 400, targetHeight: 400);
		Assert.Equal(400, output.Width);
		Assert.Equal(400, output.Height);
	}

	[Fact]
	public void WriteResizedJpeg_SmallSquare_SquareCrop_UpscalesToTarget()
	{
		using var source = new SKBitmap(300, 300);
		source.Erase(SKColors.Red);

		using var output = Resize(source, squareCrop: true, targetWidth: 400, targetHeight: 400);
		Assert.Equal(400, output.Width);
		Assert.Equal(400, output.Height);
	}

	[Fact]
	public void WriteResizedJpeg_ProductContain_FillsTargetCanvas()
	{
		using var source = new SKBitmap(400, 600);
		source.Erase(SKColors.Purple);

		using var output = Resize(source, squareCrop: false, targetWidth: 1000, targetHeight: 1200);
		Assert.Equal(1000, output.Width);
		Assert.Equal(1200, output.Height);
	}

	[Fact]
	public void DecodeImageWithOrientation_Orientation6_SwapsDimensions()
	{
		using var source = new SKBitmap(800, 600);
		source.Erase(SKColors.Coral);
		using var image = SKImage.FromBitmap(source);
		using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
		var oriented = InsertOrientation6App1(data.ToArray());

		using var stream = new MemoryStream(oriented);
		using var codec = SKCodec.Create(stream);
		Assert.NotEqual(SKEncodedOrigin.Default, codec.EncodedOrigin);

		using var decoded = SkiaImageProcessor.DecodeImageWithOrientation(new MemoryStream(oriented));
		Assert.NotNull(decoded);
		Assert.Equal(600, decoded.Width);
		Assert.Equal(800, decoded.Height);
	}

	private static SKBitmap Resize(SKBitmap source, bool squareCrop, int targetWidth, int targetHeight)
	{
		using var stream = new MemoryStream();
		SkiaImageProcessor.WriteResizedJpeg(source, stream, targetWidth, targetHeight, squareCrop);
		stream.Position = 0;
		return SKBitmap.Decode(stream);
	}

	private static FormFile CreateFormFile(byte[] content, long length, string fileName, string contentType)
		=> new(new MemoryStream(content), 0, length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = contentType };

	// Splices a minimal APP1 EXIF segment (Orientation=6, little-endian TIFF) right after the JPEG SOI,
	// keeping SkiaSharp's own JFIF APP0 — matching how real camera JPEGs carry orientation.
	private static byte[] InsertOrientation6App1(byte[] jpeg)
	{
		byte[] tiff =
		{
			0x49, 0x49,             // II (little-endian)
			0x2A, 0x00,             // TIFF magic
			0x08, 0x00, 0x00, 0x00, // offset to first IFD
			0x01, 0x00,             // 1 IFD entry
			0x12, 0x01,             // tag 0x0112 (Orientation)
			0x03, 0x00,             // type SHORT
			0x01, 0x00, 0x00, 0x00, // count 1
			0x06, 0x00, 0x00, 0x00, // value 6 (LeftTop)
			0x00, 0x00, 0x00, 0x00  // next IFD offset
		};

		var payloadLength = "Exif\0\0".Length + tiff.Length;
		var app1Length = payloadLength + 2; // includes the 2 length bytes

		using var output = new MemoryStream();
		output.WriteByte(0xFF);
		output.WriteByte(0xD8); // SOI
		output.WriteByte(0xFF);
		output.WriteByte(0xE1); // APP1
		output.WriteByte((byte)(app1Length >> 8));
		output.WriteByte((byte)app1Length);
		output.Write(System.Text.Encoding.ASCII.GetBytes("Exif\0\0"), 0, 6);
		output.Write(tiff, 0, tiff.Length);
		output.Write(jpeg, 2, jpeg.Length - 2); // original body minus its own SOI
		return output.ToArray();
	}
}