using Microsoft.AspNetCore.Http;
using VaultShop.Models;
using VaultShop.Web.Services.ImageStorage;

namespace VaultShop.Web.Services.BannerImages;

public interface IBannerImageService
{
	Task<BannerCoverVariants> SaveCoverAsync(int bannerId, IFormFile file);
	Task<StoredImage?> SaveMobileAsync(int bannerId, IFormFile file);
	void ValidateCover(int bannerId, IFormFile file);
	void ValidateMobile(int bannerId, IFormFile file);
	Task ReplaceCoverAsync(int bannerId, IFormFile file);
	Task ReplaceMobileAsync(int bannerId, IFormFile file);
	Task DeleteImagesAsync(int bannerId, params BannerImageKind[] kinds);
	Task DeleteAllAsync(int bannerId);
}
