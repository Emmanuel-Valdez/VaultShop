using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using SkiaSharp;
using VaultShop.Web.Services.CategoryImages;
using VaultShop.Web.Services.ImageStorage;

namespace VaultShop.Web.Tests;

public class CategoryImageServiceTests
{
	[Fact]
	public async Task SaveAsync_EmptyFile_RejectsWithLocalizedError()
	{
		var service = CreateService();
		var file = CreateFormFile([], "cat.jpg", "image/jpeg");
		var ex = await Assert.ThrowsAsync<CategoryImageValidationException>(() => service.SaveAsync(1, file));
		Assert.Equal("UploadFileEmpty", ex.Message);
	}

	[Fact]
	public async Task SaveAsync_OversizedFile_RejectsWithLocalizedError()
	{
		var service = CreateService();
		var file = new FormFile(new MemoryStream([1]), 0, 10 * 1024 * 1024 + 1, "file", "cat.jpg")
		{
			Headers = new HeaderDictionary(),
			ContentType = "image/jpeg"
		};
		var ex = await Assert.ThrowsAsync<CategoryImageValidationException>(() => service.SaveAsync(1, file));
		Assert.Equal("UploadFileTooLarge", ex.Message);
	}

	[Fact]
	public async Task SaveAsync_UnsupportedExtension_RejectsWithLocalizedError()
	{
		var service = CreateService();
		var file = CreateFormFile([1, 2, 3], "cat.exe", "image/jpeg");
		var ex = await Assert.ThrowsAsync<CategoryImageValidationException>(() => service.SaveAsync(1, file));
		Assert.Equal("UploadFileInvalidExtension", ex.Message);
	}

	[Fact]
	public async Task SaveAsync_UnsupportedContentType_RejectsWithLocalizedError()
	{
		var service = CreateService();
		var file = CreateFormFile([1, 2, 3], "cat.jpg", "application/pdf");
		var ex = await Assert.ThrowsAsync<CategoryImageValidationException>(() => service.SaveAsync(1, file));
		Assert.Equal("UploadFileInvalidContentType", ex.Message);
	}

	[Fact]
	public async Task SaveAsync_UndecodableImage_RejectsWithLocalizedError()
	{
		var service = CreateService();
		var file = CreateFormFile([1, 2, 3], "cat.jpg", "image/jpeg");
		var ex = await Assert.ThrowsAsync<CategoryImageValidationException>(() => service.SaveAsync(1, file));
		Assert.Equal("UploadFileInvalidImage", ex.Message);
	}

	[Fact]
	public async Task SaveAsync_ValidImage_StoresBytesThroughStorageAbstraction()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-category-image-tests-").FullName;
		try
		{
			var service = CreateService(webRootPath);
			var file = CreateFormFile(CreateValidPngBytes(), "cat.png", "image/png");

			var stored = await service.SaveAsync(42, file);

			Assert.StartsWith("images/categories/category-42/", stored.ObjectKey);
			Assert.EndsWith(".jpg", stored.ObjectKey);
			Assert.Equal("image/jpeg", stored.ContentType);
			Assert.Equal(LocalImageStorageService.ProviderName, stored.StorageProvider);
			Assert.True(stored.SizeBytes > 0);

			var savedPath = Path.Combine(webRootPath, stored.ObjectKey.Replace('/', Path.DirectorySeparatorChar));
			Assert.True(File.Exists(savedPath));
			using var saved = SKBitmap.Decode(savedPath);
			Assert.Equal(400, saved.Width);
			Assert.Equal(400, saved.Height);
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task SaveAsync_TwiceForSameCategory_ProducesDifferentKeysUnderSamePrefix()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-category-image-tests-").FullName;
		try
		{
			var service = CreateService(webRootPath);
			var png = CreateValidPngBytes();
			var first = await service.SaveAsync(7, CreateFormFile(png, "cat.png", "image/png"));
			var second = await service.SaveAsync(7, CreateFormFile(png, "cat2.png", "image/png"));
			Assert.StartsWith("images/categories/category-7/", first.ObjectKey);
			Assert.StartsWith("images/categories/category-7/", second.ObjectKey);
			Assert.NotEqual(first.ObjectKey, second.ObjectKey);
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public void Validate_EmptyFile_RejectsWithLocalizedError()
	{
		var service = CreateService();
		var file = CreateFormFile([], "cat.jpg", "image/jpeg");

		var ex = Assert.Throws<CategoryImageValidationException>(() => service.Validate(1, file));
		Assert.Equal("UploadFileEmpty", ex.Message);
	}

	[Fact]
	public void Validate_ValidImage_DoesNotThrow()
	{
		var service = CreateService();
		var file = CreateFormFile(CreateValidPngBytes(), "cat.png", "image/png");

		service.Validate(1, file);
	}

	private static CategoryImageService CreateService(string? webRootPath = null)
	{
		var env = new Mock<IWebHostEnvironment>();
		env.Setup(x => x.WebRootPath).Returns(webRootPath ?? Path.GetTempPath());
		var storage = new LocalImageStorageService(env.Object, Mock.Of<ILogger<LocalImageStorageService>>());
		var localizer = new Mock<IStringLocalizer<CategoryImageService>>();
		localizer.Setup(x => x[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key));
		return new CategoryImageService(storage, Mock.Of<ILogger<CategoryImageService>>(), localizer.Object);
	}

	private static FormFile CreateFormFile(byte[] content, string fileName, string contentType)
		=> new(new MemoryStream(content), 0, content.Length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = contentType };

	private static byte[] CreateValidPngBytes()
	{
		using var bitmap = new SKBitmap(600, 400);
		bitmap.Erase(SKColors.Red);
		using var image = SKImage.FromBitmap(bitmap);
		using var data = image.Encode(SKEncodedImageFormat.Png, 100);
		return data.ToArray();
	}
}
