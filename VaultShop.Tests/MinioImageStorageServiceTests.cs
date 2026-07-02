using VaultShop.Web.Services.ImageStorage;

namespace VaultShop.Web.Tests;

public class MinioImageStorageServiceTests
{
	[Fact]
	public void BuildPublicImageUrl_ObjectKeyWithFolders_ReturnsBucketPublicUrl()
	{
		var url = MinioImageStorageService.BuildPublicImageUrl(
			"http://localhost:9000/product-images",
			"products/product-42/image.jpg");

		Assert.Equal("http://localhost:9000/product-images/products/product-42/image.jpg", url);
	}

	[Fact]
	public void BuildPublicImageUrl_ObjectKeyWithUnsafeUrlCharacters_EscapesPathSegments()
	{
		var url = MinioImageStorageService.BuildPublicImageUrl(
			"http://localhost:9000/product-images/",
			"products/product-42/image name#.jpg");

		Assert.Equal("http://localhost:9000/product-images/products/product-42/image%20name%23.jpg", url);
	}
}
