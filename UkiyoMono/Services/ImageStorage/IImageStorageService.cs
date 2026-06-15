using UkiyoDesigns.Models;

namespace UkiyoDesignsWeb.Services.ImageStorage;

public interface IImageStorageService
{
	Task<StoredImage> SaveProductImageAsync(ImageStorageSaveRequest request, CancellationToken cancellationToken = default);
	Task DeleteProductImageAsync(ProductImage image, CancellationToken cancellationToken = default);
}
