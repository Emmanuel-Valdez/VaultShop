using Microsoft.AspNetCore.Http;
using VaultShop.Web.Services.ImageStorage;

namespace VaultShop.Web.Services.KeywordImages;

public interface IKeywordImageService
{
	Task<StoredImage> SaveChipAsync(int keywordId, IFormFile file);
	Task<CoverVariants> SaveCoverAsync(int keywordId, IFormFile file);
}