namespace VaultShop.Web.Services.ImageStorage;

public interface IImageStorageService
{
	Task<StoredImage> SaveObjectAsync(ImageStorageSaveRequest request, CancellationToken cancellationToken = default);
	Task DeleteObjectAsync(DeleteObjectRequest request, CancellationToken cancellationToken = default);
}
