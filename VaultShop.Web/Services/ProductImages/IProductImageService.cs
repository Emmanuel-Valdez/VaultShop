using Microsoft.AspNetCore.Http;

namespace VaultShop.Web.Services.ProductImages;

public interface IProductImageService
{
	Task<ProductImageUploadResult> SaveProductImagesAsync(int productId, IReadOnlyCollection<IFormFile>? files);
}
