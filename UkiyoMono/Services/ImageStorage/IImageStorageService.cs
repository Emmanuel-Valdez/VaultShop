namespace UkiyoDesignsWeb.Services.ImageStorage;

public interface IImageStorageService
{
	Task<StoredImage> SaveProductImageAsync(ImageStorageSaveRequest request, CancellationToken cancellationToken = default);
}
