using Microsoft.AspNetCore.Hosting;
using UkiyoDesigns.Models;

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

	public Task DeleteProductImageAsync(ProductImage image, CancellationToken cancellationToken = default)
	{
		var relativePath = GetSafeRelativeImagePath(image);
		if (relativePath is null)
		{
			return Task.CompletedTask;
		}

		var filePath = Path.GetFullPath(Path.Combine(_webHostEnvironment.WebRootPath, relativePath));
		var productImagesRoot = Path.GetFullPath(Path.Combine(_webHostEnvironment.WebRootPath, "images", "products"));
		var productImagesRootWithSeparator = productImagesRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

		if (!filePath.StartsWith(productImagesRootWithSeparator, StringComparison.OrdinalIgnoreCase))
		{
			return Task.CompletedTask;
		}

		if (File.Exists(filePath))
		{
			File.Delete(filePath);
		}

		return Task.CompletedTask;
	}

	private static string? GetSafeRelativeImagePath(ProductImage image)
	{
		if (!string.IsNullOrWhiteSpace(image.StorageProvider)
			&& !string.Equals(image.StorageProvider, ProviderName, StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		var storagePath = !string.IsNullOrWhiteSpace(image.ObjectKey)
			? image.ObjectKey
			: image.ImageUrl;

		if (string.IsNullOrWhiteSpace(storagePath)
			|| Uri.TryCreate(storagePath, UriKind.Absolute, out _))
		{
			return null;
		}

		var normalizedPath = storagePath
			.Replace('\\', '/')
			.TrimStart('/');

		if (!normalizedPath.StartsWith("images/products/", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		return Path.Combine(normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries));
	}
}
