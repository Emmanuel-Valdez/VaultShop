using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VaultShop.DataAccess.Data;

namespace VaultShop.Web.ViewComponents;

public sealed record StorefrontMenuLink(int Id, string Name, string? Slug);

public sealed class StorefrontMenuViewModel
{
	public required string CategoryHeading { get; init; }
	public required string CollectionHeading { get; init; }
	public IReadOnlyList<StorefrontMenuLink> Categories { get; init; } = [];
	public IReadOnlyList<StorefrontMenuLink> Collections { get; init; } = [];
}

public sealed class StorefrontMenuViewComponent : ViewComponent
{
	private readonly ApplicationDbContext _db;

	public StorefrontMenuViewComponent(ApplicationDbContext db)
	{
		_db = db;
	}

	public IViewComponentResult Invoke(string categoryHeading, string collectionHeading)
	{
		var visibleProducts = _db.Products
			.AsNoTracking()
			.Where(p => !p.IsDeleted
				&& p.IsAvailableInStore
				&& ((!p.Variants.Any() && !p.VariantValues.Any()) || p.Variants.Any(v => v.IsAvailable)));

		var categories = visibleProducts
			.Select(p => new { p.CategoryId, p.Category.Name, p.Category.Slug, p.Category.IsDeleted })
			.Where(c => !c.IsDeleted && c.Name != "")
			.Select(c => new { c.CategoryId, c.Name, c.Slug })
			.Distinct()
			.OrderBy(c => c.Name)
			.ToList()
			.Select(c => new StorefrontMenuLink(c.CategoryId, c.Name, c.Slug))
			.ToList();

		var collections = visibleProducts
			.SelectMany(p => p.Keywords)
			.Where(pk => !pk.Keyword.IsDeleted)
			.Select(pk => new { pk.KeywordId, pk.Keyword.Name, pk.Keyword.Slug })
			.Distinct()
			.OrderBy(k => k.Name)
			.ToList()
			.Select(k => new StorefrontMenuLink(k.KeywordId, k.Name, k.Slug))
			.ToList();

		return View(new StorefrontMenuViewModel
		{
			CategoryHeading = categoryHeading,
			CollectionHeading = collectionHeading,
			Categories = categories,
			Collections = collections
		});
	}
}
