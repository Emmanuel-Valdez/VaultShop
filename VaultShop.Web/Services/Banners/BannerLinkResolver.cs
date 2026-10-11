using VaultShop.Models;

namespace VaultShop.Web.Services.Banners;

public sealed record BannerTargetLink(string Action, string Controller, string Area, Dictionary<string, string> RouteValues);

public static class BannerLinkResolver
{
	public static BannerTargetLink? Resolve(Banner banner, Category? category = null, Keyword? keyword = null, Product? product = null)
	{
		return banner.TargetKind switch
		{
			BannerTargetKind.Store => new BannerTargetLink("Index", "Home", "Customer", new Dictionary<string, string>()),
			BannerTargetKind.Category when category is { IsDeleted: false } => new BannerTargetLink("Search", "Home", "Customer", new Dictionary<string, string>
			{
				["categoryId"] = category.Id.ToString(),
				["cslug"] = category.Slug ?? string.Empty,
			}),
			BannerTargetKind.Collection when keyword is { IsDeleted: false } => new BannerTargetLink("Search", "Home", "Customer", new Dictionary<string, string>
			{
				["keywordId"] = keyword.Id.ToString(),
				["slug"] = keyword.Slug ?? string.Empty,
			}),
			BannerTargetKind.Product when product is { IsDeleted: false } => new BannerTargetLink("Details", "Home", "Customer", new Dictionary<string, string>
			{
				["productId"] = product.Id.ToString(),
				["slug"] = product.Slug ?? string.Empty,
			}),
			_ => null,
		};
	}
}
