using Microsoft.AspNetCore.Http;

namespace UkiyoDesignsWeb.Services.ProductImages;

public interface IProductImageService
{
	Task<ProductImageUploadResult> SaveProductImagesAsync(int productId, IReadOnlyCollection<IFormFile>? files);
}
