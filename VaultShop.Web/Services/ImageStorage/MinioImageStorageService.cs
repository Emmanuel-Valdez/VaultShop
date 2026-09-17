using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace VaultShop.Web.Services.ImageStorage;

public sealed class MinioImageStorageService : IImageStorageService
{
	public const string ProviderName = "Minio";

	private readonly IMinioClient _minioClient;
	private readonly MinioStorageOptions _options;
	private readonly ILogger<MinioImageStorageService> _logger;

	public MinioImageStorageService(
		IMinioClient minioClient,
		IOptions<MinioStorageOptions> options,
		ILogger<MinioImageStorageService> logger)
	{
		_minioClient = minioClient;
		_options = options.Value;
		_logger = logger;
	}

	public async Task<StoredImage> SaveObjectAsync(ImageStorageSaveRequest request, CancellationToken cancellationToken = default)
	{
		ValidateRequest(request);
		ValidateOptions(_options);

		var fileName = $"{Guid.NewGuid():N}.jpg";
		var prefix = request.Prefix.Trim('/');
		var objectKey = $"{prefix}/{fileName}";

		if (request.Content.CanSeek)
		{
			request.Content.Position = 0;
		}

		await EnsureBucketExistsAsync(cancellationToken);

		var putObjectArgs = new PutObjectArgs()
			.WithBucket(_options.BucketName)
			.WithObject(objectKey)
			.WithStreamData(request.Content)
			.WithObjectSize(request.SizeBytes)
			.WithContentType(request.ContentType);

		await _minioClient.PutObjectAsync(putObjectArgs, cancellationToken);

		return new StoredImage(
			BuildPublicImageUrl(_options.PublicBaseUrl, objectKey),
			objectKey,
			fileName,
			request.ContentType,
			request.SizeBytes,
			ProviderName);
	}

	public async Task DeleteObjectAsync(DeleteObjectRequest request, CancellationToken cancellationToken = default)
	{
		if (!ShouldHandleObject(request))
		{
			_logger.LogWarning(
				"Skipped MinIO object deletion. ObjectKey: {ObjectKey}, StorageProvider: {StorageProvider}, ExpectedPrefix: {ExpectedPrefix}",
				request.ObjectKey,
				request.StorageProvider,
				request.ExpectedPrefix);
			return;
		}

		ValidateOptions(_options);

		var removeObjectArgs = new RemoveObjectArgs()
			.WithBucket(_options.BucketName)
			.WithObject(request.ObjectKey);

		await _minioClient.RemoveObjectAsync(removeObjectArgs, cancellationToken);
	}

	public static string BuildPublicImageUrl(string publicBaseUrl, string objectKey)
	{
		if (string.IsNullOrWhiteSpace(publicBaseUrl))
		{
			throw new InvalidOperationException("Missing ImageStorage:Minio:PublicBaseUrl configuration.");
		}

		if (string.IsNullOrWhiteSpace(objectKey))
		{
			throw new ArgumentException("Object key is required.", nameof(objectKey));
		}

		var escapedObjectKey = string.Join(
			'/',
			objectKey
				.Replace('\\', '/')
				.Split('/', StringSplitOptions.RemoveEmptyEntries)
				.Select(Uri.EscapeDataString));

		return $"{publicBaseUrl.TrimEnd('/')}/{escapedObjectKey}";
	}

	private async Task EnsureBucketExistsAsync(CancellationToken cancellationToken)
	{
		var bucketExistsArgs = new BucketExistsArgs()
			.WithBucket(_options.BucketName);

		if (await _minioClient.BucketExistsAsync(bucketExistsArgs, cancellationToken))
		{
			return;
		}

		var makeBucketArgs = new MakeBucketArgs()
			.WithBucket(_options.BucketName);

		await _minioClient.MakeBucketAsync(makeBucketArgs, cancellationToken);
		_logger.LogInformation("Created MinIO bucket {BucketName} because it did not exist.", _options.BucketName);
	}

	private static bool ShouldHandleObject(DeleteObjectRequest request)
	{
		if (!string.IsNullOrWhiteSpace(request.StorageProvider)
			&& !string.Equals(request.StorageProvider, ProviderName, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (string.IsNullOrWhiteSpace(request.ObjectKey)
			|| Uri.TryCreate(request.ObjectKey, UriKind.Absolute, out _))
		{
			return false;
		}

		var prefix = request.ExpectedPrefix?.Trim('/');
		if (string.IsNullOrWhiteSpace(prefix))
		{
			return false;
		}

		var normalizedObjectKey = request.ObjectKey.Replace('\\', '/').TrimStart('/');
		return normalizedObjectKey.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)
			&& !normalizedObjectKey.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..");
	}

	private static void ValidateRequest(ImageStorageSaveRequest request)
	{
		ArgumentNullException.ThrowIfNull(request.Content);

		if (string.IsNullOrWhiteSpace(request.Prefix))
		{
			throw new ArgumentException("Prefix is required.", nameof(request));
		}

		if (request.SizeBytes <= 0)
		{
			throw new ArgumentException("Image size must be greater than zero.", nameof(request));
		}

		if (string.IsNullOrWhiteSpace(request.ContentType))
		{
			throw new ArgumentException("Content type is required.", nameof(request));
		}
	}

	private static void ValidateOptions(MinioStorageOptions options)
	{
		if (string.IsNullOrWhiteSpace(options.Endpoint))
		{
			throw new InvalidOperationException("Missing ImageStorage:Minio:Endpoint configuration.");
		}

		if (string.IsNullOrWhiteSpace(options.AccessKey))
		{
			throw new InvalidOperationException("Missing ImageStorage:Minio:AccessKey configuration.");
		}

		if (string.IsNullOrWhiteSpace(options.SecretKey))
		{
			throw new InvalidOperationException("Missing ImageStorage:Minio:SecretKey configuration.");
		}

		if (string.IsNullOrWhiteSpace(options.BucketName))
		{
			throw new InvalidOperationException("Missing ImageStorage:Minio:BucketName configuration.");
		}

		if (string.IsNullOrWhiteSpace(options.PublicBaseUrl))
		{
			throw new InvalidOperationException("Missing ImageStorage:Minio:PublicBaseUrl configuration.");
		}
	}
}
