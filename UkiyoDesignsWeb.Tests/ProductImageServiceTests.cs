using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using SkiaSharp;
using UkiyoDesignsWeb.Services.ProductImages;

namespace UkiyoDesignsWeb.Tests
{
	public class ProductImageServiceTests
	{
		[Fact]
		public async Task SaveProductImagesAsync_NoFiles_ReturnsNoErrorsAndNoSavedImages()
		{
			var service = CreateService();

			var result = await service.SaveProductImagesAsync(1, []);
			Assert.False(result.HasErrors);
			Assert.Empty(result.SavedImageUrls);

		}

		[Fact]
		public async Task SaveProductImagesAsync_EmptyFile_ReturnsValidationError()
		{
			var service = CreateService();
			var file = CreateFormFile([], "empty.jpg", "image/jpeg");

			var result = await service.SaveProductImagesAsync(1, [file]);

			Assert.True(result.HasErrors);
			Assert.Contains("UploadFileEmpty", result.Errors);
			Assert.Empty(result.SavedImageUrls);
		}

		[Fact]
		public async Task SaveProductImagesAsync_InvalidExtension_ReturnsValidationError()
		{
			var service = CreateService();
			var file = CreateFormFile([1, 2, 3], "image.exe", "image/jpeg");

			var result = await service.SaveProductImagesAsync(1, [file]);

			Assert.True(result.HasErrors);
			Assert.Contains("UploadFileInvalidExtension", result.Errors);
			Assert.Empty(result.SavedImageUrls);
		}

		[Fact]
		public async Task SaveProductImagesAsync_InvalidContentType_ReturnsValidationError()
		{
			var service = CreateService();
			var file = CreateFormFile([1, 2, 3], "image.jpg", "application/pdf");

			var result = await service.SaveProductImagesAsync(1, [file]);

			Assert.True(result.HasErrors);
			Assert.Contains("UploadFileInvalidContentType", result.Errors);
			Assert.Empty(result.SavedImageUrls);
		}

		[Fact]
		public async Task SaveProductImagesAsync_UndecodableImage_ReturnsValidationError()
		{
			var service = CreateService();
			var file = CreateFormFile([1, 2, 3], "image.jpg", "image/jpeg");

			var result = await service.SaveProductImagesAsync(1, [file]);

			Assert.True(result.HasErrors);
			Assert.Contains("UploadFileInvalidImage", result.Errors);
			Assert.Empty(result.SavedImageUrls);
		}

		[Fact]
		public async Task SaveProductImagesAsync_ValidImage_SavesImageWithGeneratedSafeUrl()
		{
			var webRootPath = Directory.CreateTempSubdirectory("ukiyo-product-image-tests-").FullName;
			try
			{
				var service = CreateService(webRootPath);
				var file = CreateFormFile(CreateValidPngBytes(), "original-product-name.png", "image/png");

				var result = await service.SaveProductImagesAsync(42, [file]);

				var savedImageUrl = Assert.Single(result.SavedImageUrls);
				Assert.False(result.HasErrors);
				Assert.StartsWith("\\images\\products\\product-42\\", savedImageUrl);
				Assert.EndsWith(".jpg", savedImageUrl);
				Assert.DoesNotContain("original-product-name", savedImageUrl);

				var savedImagePath = Path.Combine(webRootPath, savedImageUrl.TrimStart('\\').Replace('\\', Path.DirectorySeparatorChar));
				Assert.True(File.Exists(savedImagePath));
			}
			finally
			{
				Directory.Delete(webRootPath, recursive: true);
			}
		}

		private static ProductImageService CreateService(string? webRootPath = null)
		{
			var environment = new Mock<IWebHostEnvironment>();
			environment.Setup(x => x.WebRootPath).Returns(webRootPath ?? Path.GetTempPath());
			var logger = Mock.Of<ILogger<ProductImageService>>();

			var localizer = new Mock<IStringLocalizer<ProductImageService>>();
			localizer.Setup(x => x[It.IsAny<string>()])
				.Returns((string key) => new LocalizedString(key, key));
			return new ProductImageService(environment.Object, logger, localizer.Object);
		}

		private static FormFile CreateFormFile(byte[] content, string fileName, string contentType)
		{
			return new FormFile(
				new MemoryStream(content),
				0,
				content.Length,
				"file",
				fileName)
			{
				Headers = new HeaderDictionary(),
				ContentType = contentType
			};
		}

		private static byte[] CreateValidPngBytes()
		{
			using var bitmap = new SKBitmap(10, 10);
			bitmap.Erase(SKColors.Red);
			using var image = SKImage.FromBitmap(bitmap);
			using var data = image.Encode(SKEncodedImageFormat.Png, 100);
			return data.ToArray();
		}

	}

}
