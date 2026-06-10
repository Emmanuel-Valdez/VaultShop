using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
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
			var file = new FormFile(
				new MemoryStream([]),
				0,
				0,
				"file",
				"empty.jpg")
			{
				Headers = new HeaderDictionary(),
				ContentType = "image/jpeg"
			};

			var result = await service.SaveProductImagesAsync(1, [file]);

			Assert.True(result.HasErrors);
			Assert.Contains("UploadFileEmpty", result.Errors);
			Assert.Empty(result.SavedImageUrls);
		}

		private static ProductImageService CreateService()
		{
			var environment = new Mock<IWebHostEnvironment>();
			environment.Setup(x => x.WebRootPath).Returns(Path.GetTempPath());
			var logger = Mock.Of<ILogger<ProductImageService>>();

			var localizer = new Mock<IStringLocalizer<ProductImageService>>();
			localizer.Setup(x => x[It.IsAny<string>()])
				.Returns((string key) => new LocalizedString(key, key));
			return new ProductImageService(environment.Object, logger, localizer.Object);
		}

	}

}
