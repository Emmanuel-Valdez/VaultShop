using Microsoft.Extensions.DependencyInjection;
using VaultShop.DataAccess.Data;
using VaultShop.Models;

namespace VaultShop.Web.Tests;

public class StorefrontMenuHttpTests
{
	[Fact]
	public async Task HomeMenu_ListsOnlyVisibleSellableCategoriesAndCollections()
	{
		using var factory = new CustomWebApplicationFactory();
		var ids = SeedCatalog(factory);
		var client = factory.CreateClient();

		var body = await client.GetStringAsync("/en-US/Customer/Home/Index");

		Assert.Contains("storefront-mobile-nav", body);
		Assert.Contains("data-bs-target=\"#sideNavbarTop\"", body);
		Assert.Contains("data-bs-target=\"#storefrontSearch\"", body);
		Assert.Contains("id=\"storefrontSearch\"", body);
		Assert.Contains("name=\"searchString\"", body);
		Assert.Contains($"categoryId={ids.VisibleCategoryId}", body);
		Assert.Contains("cslug=visible-category", body);
		Assert.Contains($"keywordId={ids.VisibleCollectionId}", body);
		Assert.Contains("slug=visible-collection", body);
		Assert.DoesNotContain("Hidden Category", body);
		Assert.DoesNotContain("Hidden Collection", body);
		Assert.DoesNotContain("Deleted Collection", body);
		Assert.DoesNotContain("/Admin/Order", body);
	}

	[Fact]
	public async Task SignedInCustomer_SeesOrderLink()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

		var body = await client.GetStringAsync("/en-US/Customer/Home/Index");

		Assert.Contains("/Admin/Order", body);
	}

	[Fact]
	public async Task IdentityLogin_HidesStorefrontBottomNav()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient();

		var body = await client.GetStringAsync("/Identity/Account/Login");

		Assert.DoesNotContain("storefront-mobile-nav", body);
		Assert.DoesNotContain("storefront-search", body);
		Assert.Contains("data-bs-target=\"#sideNavbarTop\"", body);
	}

	private static (int VisibleCategoryId, int VisibleCollectionId) SeedCatalog(CustomWebApplicationFactory factory)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var visibleCategory = NewCategory("Visible Category", "visible-category");
		var hiddenCategory = NewCategory("Hidden Category", "hidden-category");
		db.Categories.AddRange(visibleCategory, hiddenCategory);

		var visibleCollection = new Keyword { Name = "Visible Collection", Slug = "visible-collection" };
		var hiddenCollection = new Keyword { Name = "Hidden Collection", Slug = "hidden-collection" };
		var deletedCollection = new Keyword { Name = "Deleted Collection", Slug = "deleted-collection", IsDeleted = true };
		db.Keywords.AddRange(visibleCollection, hiddenCollection, deletedCollection);
		db.SaveChanges();

		var visibleProduct = NewProduct("Visible Product", visibleCategory.Id);
		var unavailableProduct = NewProduct("Unavailable Product", hiddenCategory.Id, isAvailable: false);
		db.Products.AddRange(visibleProduct, unavailableProduct);
		db.SaveChanges();

		db.ProductKeywords.AddRange(
			new ProductKeyword { ProductId = visibleProduct.Id, KeywordId = visibleCollection.Id },
			new ProductKeyword { ProductId = unavailableProduct.Id, KeywordId = hiddenCollection.Id },
			new ProductKeyword { ProductId = visibleProduct.Id, KeywordId = deletedCollection.Id });
		db.SaveChanges();

		return (visibleCategory.Id, visibleCollection.Id);
	}

	private static Category NewCategory(string name, string slug) => new()
	{
		Name = name,
		Slug = slug,
		AvgShippingCost = 100m,
	};

	private static Product NewProduct(string name, int categoryId, bool isAvailable = true) => new()
	{
		Name = name,
		Description = name,
		MaxExpectation = 10,
		CategoryId = categoryId,
		ListPrice = 100m,
		FinalRetailPrice = 100m,
		FinalWholesalePrice = 100m,
		IsAvailableInStore = isAvailable,
		IsDeleted = false,
		StockQuantity = 3,
	};
}
