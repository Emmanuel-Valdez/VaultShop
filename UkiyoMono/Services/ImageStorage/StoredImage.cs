namespace UkiyoDesignsWeb.Services.ImageStorage;

public sealed record StoredImage(
	string ImageUrl,
	string ObjectKey,
	string FileName,
	string ContentType,
	long SizeBytes,
	string StorageProvider);
