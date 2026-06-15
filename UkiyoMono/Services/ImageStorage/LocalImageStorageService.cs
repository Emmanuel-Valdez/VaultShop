using Microsoft.AspNetCore.Hosting;

namespace UkiyoDesignsWeb.Services.ImageStorage;

public sealed class LocalImageStorageService : IImageStorageService
{
	public const string ProviderName = "LocalFileSystem";

	private readonly IWebHostEnvironment _webHostEnvironment;

	public LocalImageStorageService(IWebHostEnvironment webHostEnvironment)
	{
		_webHostEnvironment = webHostEnvironment;
	}

	public async Task<StoredImage> SaveProductImageAsync(ImageStorageSaveRequest request, CancellationToken cancellationToken = default)
	{
		var fileName = $"{Guid.NewGuid():N}.jpg";
		var objectKey = $"images/products/product-{request.ProductId}/{fileName}";
		var productPath = Path.Combine("images", "products", $"product-{request.ProductId}");
		var finalPath = Path.Combine(_webHostEnvironment.WebRootPath, productPath);

		Directory.CreateDirectory(finalPath);

		var outputFilePath = Path.Combine(finalPath, fileName);
		await using (var outputStream = new FileStream(outputFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
		{
			if (request.Content.CanSeek)
			{
				request.Content.Position = 0;
			}

			await request.Content.CopyToAsync(outputStream, cancellationToken);
		}

		var imageUrl = "\\" + objectKey.Replace('/', '\\');

		return new StoredImage(
			imageUrl,
			objectKey,
			fileName,
			"image/jpeg",
			request.SizeBytes,
			ProviderName);
	}
}
