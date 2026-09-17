namespace VaultShop.Web.Services.ImageStorage;

public sealed record ImageStorageSaveRequest(
	string Prefix,
	Stream Content,
	string FileName,
	string ContentType,
	long SizeBytes);
