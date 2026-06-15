using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using UkiyoDesigns.Models;
using UkiyoDesignsWeb.Services.ImageStorage;

namespace UkiyoDesignsWeb.Tests
{
	public class LocalImageStorageServiceTests
	{
		[Fact]
		public async Task DeleteProductImageAsync_ObjectKeyLocalImage_DeletesFile()
		{
			var webRootPath = Directory.CreateTempSubdirectory("ukiyo-local-storage-tests-").FullName;
			try
			{
				var filePath = CreateProductImageFile(webRootPath, "product-1", "image.jpg");
				var service = CreateService(webRootPath);

				await service.DeleteProductImageAsync(new ProductImage
				{
					ObjectKey = "images/products/product-1/image.jpg",
					StorageProvider = LocalImageStorageService.ProviderName
				});

				Assert.False(File.Exists(filePath));
			}
			finally
			{
				Directory.Delete(webRootPath, recursive: true);
			}
		}

		[Fact]
		public async Task DeleteProductImageAsync_MissingObjectKey_DoesNotDeleteFile()
		{
			var webRootPath = Directory.CreateTempSubdirectory("ukiyo-local-storage-tests-").FullName;
			try
			{
				var filePath = CreateProductImageFile(webRootPath, "product-2", "image.jpg");
				var service = CreateService(webRootPath);

				await service.DeleteProductImageAsync(new ProductImage
				{
					ImageUrl = "\\images\\products\\product-2\\image.jpg"
				});

				Assert.True(File.Exists(filePath));
			}
			finally
			{
				Directory.Delete(webRootPath, recursive: true);
			}
		}

		[Fact]
		public async Task DeleteProductImageAsync_TraversalObjectKey_DoesNotDeleteOutsideProductImagesRoot()
		{
			var webRootPath = Directory.CreateTempSubdirectory("ukiyo-local-storage-tests-").FullName;
			try
			{
				var protectedFilePath = Path.Combine(webRootPath, "protected.txt");
				await File.WriteAllTextAsync(protectedFilePath, "do not delete");
				var service = CreateService(webRootPath);

				await service.DeleteProductImageAsync(new ProductImage
				{
					ObjectKey = "images/products/../../protected.txt",
					StorageProvider = LocalImageStorageService.ProviderName
				});

				Assert.True(File.Exists(protectedFilePath));
			}
			finally
			{
				Directory.Delete(webRootPath, recursive: true);
			}
		}

		private static LocalImageStorageService CreateService(string webRootPath)
		{
			var environment = new Mock<IWebHostEnvironment>();
			environment.Setup(x => x.WebRootPath).Returns(webRootPath);
			return new LocalImageStorageService(environment.Object, Mock.Of<ILogger<LocalImageStorageService>>());
		}

		private static string CreateProductImageFile(string webRootPath, string productFolder, string fileName)
		{
			var folderPath = Path.Combine(webRootPath, "images", "products", productFolder);
			Directory.CreateDirectory(folderPath);
			var filePath = Path.Combine(folderPath, fileName);
			File.WriteAllText(filePath, "image bytes");
			return filePath;
		}
	}
}
