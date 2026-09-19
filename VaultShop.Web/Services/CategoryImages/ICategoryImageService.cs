using Microsoft.AspNetCore.Http;
using VaultShop.Web.Services.ImageStorage;

namespace VaultShop.Web.Services.CategoryImages;

public interface ICategoryImageService
{
	void Validate(int categoryId, IFormFile file);

	Task<StoredImage> SaveAsync(int categoryId, IFormFile file);
}
