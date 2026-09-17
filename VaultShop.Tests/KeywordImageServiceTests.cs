using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using SkiaSharp;
using VaultShop.Web.Services.ImageStorage;
using VaultShop.Web.Services.KeywordImages;

namespace VaultShop.Web.Tests;

public class KeywordImageServiceTests
{
	[Fact]
	public async Task SaveChipAsync_EmptyFile_RejectsWithLocalizedError()
	{
		var service = CreateService();
		var file = CreateFormFile([], "chip.jpg", "image/jpeg");

		var ex = await Assert.ThrowsAsync<KeywordImageValidationException>(() => service.SaveChipAsync(1, file));

		Assert.Equal("UploadFileEmpty", ex.Message);
	}

	[Fact]
	public async Task SaveChipAsync_OversizedFile_RejectsWithLocalizedError()
	{
		var service = CreateService();
		var file = new FormFile(new MemoryStream([1]), 0, 10 * 1024 * 1024 + 1, "file", "chip.jpg")
		{
			Headers = new HeaderDictionary(),
			ContentType = "image/jpeg"
		};

		var ex = await Assert.ThrowsAsync<KeywordImageValidationException>(() => service.SaveChipAsync(1, file));

		Assert.Equal("UploadFileTooLarge", ex.Message);
	}

	[Fact]
	public async Task SaveChipAsync_UnsupportedExtension_RejectsWithLocalizedError()
	{
		var service = CreateService();
		var file = CreateFormFile([1, 2, 3], "chip.exe", "image/jpeg");

		var ex = await Assert.ThrowsAsync<KeywordImageValidationException>(() => service.SaveChipAsync(1, file));

		Assert.Equal("UploadFileInvalidExtension", ex.Message);
	}

	[Fact]
	public async Task SaveChipAsync_UnsupportedContentType_RejectsWithLocalizedError()
	{
		var service = CreateService();
		var file = CreateFormFile([1, 2, 3], "chip.jpg", "application/pdf");

		var ex = await Assert.ThrowsAsync<KeywordImageValidationException>(() => service.SaveChipAsync(1, file));

		Assert.Equal("UploadFileInvalidContentType", ex.Message);
	}

	[Fact]
	public async Task SaveChipAsync_UndecodableImage_RejectsWithLocalizedError()
	{
		var service = CreateService();
		var file = CreateFormFile([1, 2, 3], "chip.jpg", "image/jpeg");

		var ex = await Assert.ThrowsAsync<KeywordImageValidationException>(() => service.SaveChipAsync(1, file));

		Assert.Equal("UploadFileInvalidImage", ex.Message);
	}

	[Fact]
	public async Task SaveChipAsync_ValidImage_StoresBytesThroughStorageAbstraction()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-keyword-image-tests-").FullName;
		try
		{
			var service = CreateService(webRootPath);
			var file = CreateFormFile(CreateValidPngBytes(), "chip.png", "image/png");

			var stored = await service.SaveChipAsync(42, file);

			Assert.StartsWith("images/keywords/keyword-42/", stored.ObjectKey);
			Assert.EndsWith(".jpg", stored.ObjectKey);
			Assert.Equal("image/jpeg", stored.ContentType);
			Assert.Equal(LocalImageStorageService.ProviderName, stored.StorageProvider);
			Assert.True(stored.SizeBytes > 0);

			var savedPath = Path.Combine(webRootPath, stored.ObjectKey.Replace('/', Path.DirectorySeparatorChar));
			Assert.True(File.Exists(savedPath));
			using var savedChip = SKBitmap.Decode(savedPath);
			Assert.Equal(400, savedChip.Width);
			Assert.Equal(400, savedChip.Height);
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task ChipCoverAndReplace_PersistIndependentlyUnderSamePrefix()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-keyword-image-tests-").FullName;
		try
		{
			var service = CreateService(webRootPath);
			var png = CreateValidPngBytes();

			var chip = await service.SaveChipAsync(7, CreateFormFile(png, "chip.png", "image/png"));
			var cover = await service.SaveCoverAsync(7, CreateFormFile(png, "cover.png", "image/png"));
			var replacedChip = await service.SaveChipAsync(7, CreateFormFile(png, "chip2.png", "image/png"));

			Assert.StartsWith("images/keywords/keyword-7/", chip.ObjectKey);
			Assert.StartsWith("images/keywords/keyword-7/", cover.ObjectKey);
			Assert.StartsWith("images/keywords/keyword-7/", replacedChip.ObjectKey);

			Assert.NotEqual(chip.ObjectKey, cover.ObjectKey);
			Assert.NotEqual(chip.ObjectKey, replacedChip.ObjectKey);

			Assert.True(File.Exists(Path.Combine(webRootPath, replacedChip.ObjectKey.Replace('/', Path.DirectorySeparatorChar))));

			using var savedCover = SKBitmap.Decode(Path.Combine(webRootPath, cover.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
			Assert.Equal(1400, savedCover.Width);
			Assert.Equal(500, savedCover.Height);
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	private static KeywordImageService CreateService(string? webRootPath = null)
	{
		var environment = new Mock<IWebHostEnvironment>();
		environment.Setup(x => x.WebRootPath).Returns(webRootPath ?? Path.GetTempPath());
		var imageStorageService = new LocalImageStorageService(environment.Object, Mock.Of<ILogger<LocalImageStorageService>>());

		var localizer = new Mock<IStringLocalizer<KeywordImageService>>();
		localizer.Setup(x => x[It.IsAny<string>()])
			.Returns((string key) => new LocalizedString(key, key));

		return new KeywordImageService(imageStorageService, Mock.Of<ILogger<KeywordImageService>>(), localizer.Object);
	}

	private static FormFile CreateFormFile(byte[] content, string fileName, string contentType)
	{
		return new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName)
		{
			Headers = new HeaderDictionary(),
			ContentType = contentType
		};
	}

	private static byte[] CreateValidPngBytes()
	{
		using var bitmap = new SKBitmap(600, 400);
		bitmap.Erase(SKColors.Red);
		using var image = SKImage.FromBitmap(bitmap);
		using var data = image.Encode(SKEncodedImageFormat.Png, 100);
		return data.ToArray();
	}
}
