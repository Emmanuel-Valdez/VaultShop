namespace UkiyoDesignsWeb.Services.ImageStorage;

public sealed record ImageStorageSaveRequest(
	int ProductId,
	Stream Content,
	string FileName,
	string ContentType,
	long SizeBytes);
