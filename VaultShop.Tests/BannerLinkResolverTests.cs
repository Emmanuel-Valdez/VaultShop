using VaultShop.Models;
using VaultShop.Web.Services.Banners;

namespace VaultShop.Web.Tests;

public class BannerLinkResolverTests
{
	[Fact]
	public void Resolve_StoreTarget_PointsToHomeIndex()
	{
		var link = BannerLinkResolver.Resolve(new Banner { TargetKind = BannerTargetKind.Store });

		Assert.NotNull(link);
		Assert.Equal("Index", link.Action);
		Assert.Equal("Home", link.Controller);
		Assert.Equal("Customer", link.Area);
		Assert.Empty(link.RouteValues);
	}

	[Fact]
	public void Resolve_CategoryTarget_PointsToFilteredSearch()
	{
		var link = BannerLinkResolver.Resolve(
			new Banner { TargetKind = BannerTargetKind.Category, TargetId = 4 },
			category: new Category { Id = 4, Name = "Abrigos", Slug = "abrigos" });

		Assert.NotNull(link);
		Assert.Equal("Search", link.Action);
		Assert.Equal("4", link.RouteValues["categoryId"]);
		Assert.Equal("abrigos", link.RouteValues["cslug"]);
	}

	[Fact]
	public void Resolve_CollectionTarget_PointsToFilteredSearch()
	{
		var link = BannerLinkResolver.Resolve(
			new Banner { TargetKind = BannerTargetKind.Collection, TargetId = 9 },
			keyword: new Keyword { Id = 9, Name = "Invierno", Slug = "invierno" });

		Assert.NotNull(link);
		Assert.Equal("Search", link.Action);
		Assert.Equal("9", link.RouteValues["keywordId"]);
		Assert.Equal("invierno", link.RouteValues["slug"]);
	}

	[Fact]
	public void Resolve_ProductTarget_PointsToDetails()
	{
		var link = BannerLinkResolver.Resolve(
			new Banner { TargetKind = BannerTargetKind.Product, TargetId = 12 },
			product: new Product { Id = 12, Slug = "gabardina-acrilica" });

		Assert.NotNull(link);
		Assert.Equal("Details", link.Action);
		Assert.Equal("12", link.RouteValues["productId"]);
		Assert.Equal("gabardina-acrilica", link.RouteValues["slug"]);
	}

	[Theory]
	[InlineData(BannerTargetKind.Category)]
	[InlineData(BannerTargetKind.Collection)]
	[InlineData(BannerTargetKind.Product)]
	public void Resolve_MissingTarget_RendersUnlinked(BannerTargetKind kind)
	{
		Assert.Null(BannerLinkResolver.Resolve(new Banner { TargetKind = kind, TargetId = 1 }));
	}

	[Fact]
	public void Resolve_DeletedCategory_RendersUnlinked()
	{
		var link = BannerLinkResolver.Resolve(
			new Banner { TargetKind = BannerTargetKind.Category, TargetId = 4 },
			category: new Category { Id = 4, Name = "Abrigos", IsDeleted = true });

		Assert.Null(link);
	}

	[Fact]
	public void Resolve_DeletedCollection_RendersUnlinked()
	{
		var link = BannerLinkResolver.Resolve(
			new Banner { TargetKind = BannerTargetKind.Collection, TargetId = 9 },
			keyword: new Keyword { Id = 9, Name = "Invierno", IsDeleted = true });

		Assert.Null(link);
	}

	[Fact]
	public void Resolve_DeletedProduct_RendersUnlinked()
	{
		var link = BannerLinkResolver.Resolve(
			new Banner { TargetKind = BannerTargetKind.Product, TargetId = 12 },
			product: new Product { Id = 12, IsDeleted = true });

		Assert.Null(link);
	}
}
