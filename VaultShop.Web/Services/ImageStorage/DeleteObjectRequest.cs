namespace VaultShop.Web.Services.ImageStorage;

public sealed record DeleteObjectRequest(
	string ObjectKey,
	string StorageProvider,
	string ExpectedPrefix);