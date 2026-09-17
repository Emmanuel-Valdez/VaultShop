using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using VaultShop.Web.Services.ImageStorage;

namespace VaultShop.Web.Tests
{
	public class LocalImageStorageServiceTests
	{
		[Fact]
		public async Task DeleteObjectAsync_ObjectKeyLocalImage_DeletesFile()
		{
			var webRootPath = Directory.CreateTempSubdirectory("vaultshop-local-storage-tests-").FullName;
			try
			{
				var filePath = CreateProductImageFile(webRootPath, "product-1", "image.jpg");
				var service = CreateService(webRootPath);

				await service.DeleteObjectAsync(new DeleteObjectRequest(
					"images/products/product-1/image.jpg",
					LocalImageStorageService.ProviderName,
					"products/"));

				Assert.False(File.Exists(filePath));
			}
			finally
			{
				Directory.Delete(webRootPath, recursive: true);
			}
		}

		[Fact]
		public async Task DeleteObjectAsync_MissingObjectKey_DoesNotDeleteFile()
		{
			var webRootPath = Directory.CreateTempSubdirectory("vaultshop-local-storage-tests-").FullName;
			try
			{
				var filePath = CreateProductImageFile(webRootPath, "product-2", "image.jpg");
				var service = CreateService(webRootPath);

				await service.DeleteObjectAsync(new DeleteObjectRequest(
					string.Empty,
					LocalImageStorageService.ProviderName,
					"products/"));

				Assert.True(File.Exists(filePath));
			}
			finally
			{
				Directory.Delete(webRootPath, recursive: true);
			}
		}

		[Fact]
		public async Task DeleteObjectAsync_TraversalObjectKey_DoesNotDeleteOutsideExpectedRoot()
		{
			var webRootPath = Directory.CreateTempSubdirectory("vaultshop-local-storage-tests-").FullName;
			try
			{
				var protectedFilePath = Path.Combine(webRootPath, "protected.txt");
				await File.WriteAllTextAsync(protectedFilePath, "do not delete");
				var service = CreateService(webRootPath);

				await service.DeleteObjectAsync(new DeleteObjectRequest(
					"images/products/../../protected.txt",
					LocalImageStorageService.ProviderName,
					"products/"));

				Assert.True(File.Exists(protectedFilePath));
			}
			finally
			{
				Directory.Delete(webRootPath, recursive: true);
			}
		}

		[Fact]
		public async Task DeleteObjectAsync_WrongExpectedPrefix_DoesNotDeleteFile()
		{
			var webRootPath = Directory.CreateTempSubdirectory("vaultshop-local-storage-tests-").FullName;
			try
			{
				var filePath = CreateProductImageFile(webRootPath, "product-3", "image.jpg");
				var service = CreateService(webRootPath);

				await service.DeleteObjectAsync(new DeleteObjectRequest(
					"images/products/product-3/image.jpg",
					LocalImageStorageService.ProviderName,
					"keywords/"));

				Assert.True(File.Exists(filePath));
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
