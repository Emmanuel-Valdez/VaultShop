using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using UkiyoDesigns.Models;

namespace UkiyoDesignsWeb.Services.ImageStorage;

public sealed class LocalImageStorageService : IImageStorageService
{
	public const string ProviderName = "LocalFileSystem";

	private readonly IWebHostEnvironment _webHostEnvironment;
	private readonly ILogger<LocalImageStorageService> _logger;

	public LocalImageStorageService(IWebHostEnvironment webHostEnvironment, ILogger<LocalImageStorageService> logger)
	{
		_webHostEnvironment = webHostEnvironment;
		_logger = logger;
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
			_logger.LogWarning(
				"Skipped local product image deletion for product image {ProductImageId}, product {ProductId}. ObjectKey: {ObjectKey}, StorageProvider: {StorageProvider}",
				image.Id,
				image.ProductId,
				image.ObjectKey,
				image.StorageProvider);
			return Task.CompletedTask;
		}

		var filePath = Path.GetFullPath(Path.Combine(_webHostEnvironment.WebRootPath, relativePath));
		var productImagesRoot = Path.GetFullPath(Path.Combine(_webHostEnvironment.WebRootPath, "images", "products"));
		var productImagesRootWithSeparator = productImagesRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

		if (!filePath.StartsWith(productImagesRootWithSeparator, StringComparison.OrdinalIgnoreCase))
		{
			_logger.LogWarning(
				"Rejected local product image deletion outside product image root for product image {ProductImageId}, product {ProductId}. ObjectKey: {ObjectKey}",
				image.Id,
				image.ProductId,
				image.ObjectKey);
			return Task.CompletedTask;
		}

		if (File.Exists(filePath))
		{
			File.Delete(filePath);
		}
		else
		{
			_logger.LogInformation(
				"Local product image file was already missing for product image {ProductImageId}, product {ProductId}. ObjectKey: {ObjectKey}",
				image.Id,
				image.ProductId,
				image.ObjectKey);
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

		if (string.IsNullOrWhiteSpace(image.ObjectKey)
			|| Uri.TryCreate(image.ObjectKey, UriKind.Absolute, out _))
		{
			return null;
		}

		var normalizedPath = image.ObjectKey
			.Replace('\\', '/')
			.TrimStart('/');

		if (!normalizedPath.StartsWith("images/products/", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		return Path.Combine(normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries));
	}
}
