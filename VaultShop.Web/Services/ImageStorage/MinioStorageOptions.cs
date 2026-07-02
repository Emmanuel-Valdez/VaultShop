namespace VaultShop.Web.Services.ImageStorage;

public sealed class MinioStorageOptions
{
	public string Endpoint { get; init; } = string.Empty;
	public bool UseSsl { get; init; }
	public string BucketName { get; init; } = string.Empty;
	public string AccessKey { get; init; } = string.Empty;
	public string SecretKey { get; init; } = string.Empty;
	public string PublicBaseUrl { get; init; } = string.Empty;
}
