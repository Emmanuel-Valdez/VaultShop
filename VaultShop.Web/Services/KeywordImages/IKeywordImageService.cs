using Microsoft.AspNetCore.Http;
using VaultShop.Web.Services.ImageStorage;

namespace VaultShop.Web.Services.KeywordImages;

public interface IKeywordImageService
{
	Task<StoredImage> SaveChipAsync(int keywordId, IFormFile file);
	Task<StoredImage> SaveCoverAsync(int keywordId, IFormFile file);
}