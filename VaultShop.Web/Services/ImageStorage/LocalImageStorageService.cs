using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace VaultShop.Web.Services.ImageStorage;

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

	public async Task<StoredImage> SaveObjectAsync(ImageStorageSaveRequest request, CancellationToken cancellationToken = default)
	{
		var fileName = $"{Guid.NewGuid():N}.jpg";
		var prefix = request.Prefix.Trim('/');
		var objectKey = $"images/{prefix}/{fileName}";
		var directoryPath = Path.Combine("images", string.Join(Path.DirectorySeparatorChar, prefix.Split('/')));
		var finalPath = Path.Combine(_webHostEnvironment.WebRootPath, directoryPath);

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

	public Task DeleteObjectAsync(DeleteObjectRequest request, CancellationToken cancellationToken = default)
	{
		var relativePath = GetSafeRelativeImagePath(request);
		if (relativePath is null)
		{
			_logger.LogWarning(
				"Skipped local object deletion. ObjectKey: {ObjectKey}, StorageProvider: {StorageProvider}, ExpectedPrefix: {ExpectedPrefix}",
				request.ObjectKey,
				request.StorageProvider,
				request.ExpectedPrefix);
			return Task.CompletedTask;
		}

		var prefix = request.ExpectedPrefix.Trim('/');
		var filePath = Path.GetFullPath(Path.Combine(_webHostEnvironment.WebRootPath, relativePath));
		var objectRoot = Path.GetFullPath(Path.Combine(_webHostEnvironment.WebRootPath, "images", prefix));
		var objectRootWithSeparator = objectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

		if (!filePath.StartsWith(objectRootWithSeparator, StringComparison.OrdinalIgnoreCase))
		{
			_logger.LogWarning(
				"Rejected local object deletion outside expected root for ObjectKey: {ObjectKey}. ExpectedPrefix: {ExpectedPrefix}",
				request.ObjectKey,
				request.ExpectedPrefix);
			return Task.CompletedTask;
		}

		if (File.Exists(filePath))
		{
			File.Delete(filePath);
		}
		else
		{
			_logger.LogInformation(
				"Local object file was already missing for ObjectKey: {ObjectKey}",
				request.ObjectKey);
		}

		return Task.CompletedTask;
	}

	private static string? GetSafeRelativeImagePath(DeleteObjectRequest request)
	{
		if (!string.IsNullOrWhiteSpace(request.StorageProvider)
			&& !string.Equals(request.StorageProvider, ProviderName, StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		if (string.IsNullOrWhiteSpace(request.ObjectKey)
			|| Uri.TryCreate(request.ObjectKey, UriKind.Absolute, out _))
		{
			return null;
		}

		var expectedPrefix = request.ExpectedPrefix?.Trim('/');
		if (string.IsNullOrWhiteSpace(expectedPrefix))
		{
			return null;
		}

		var normalizedPath = request.ObjectKey
			.Replace('\\', '/')
			.TrimStart('/');

		if (!normalizedPath.StartsWith($"images/{expectedPrefix}/", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		return Path.Combine(normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries));
	}
}
