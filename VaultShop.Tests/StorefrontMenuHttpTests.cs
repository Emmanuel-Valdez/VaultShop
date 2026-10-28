using System.Net;
using System.Text.RegularExpressions;
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
	public async Task IdentityLogin_ShowsMobileShellWithoutDuplicateMenuControl()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient();

		var body = await client.GetStringAsync("/Identity/Account/Login");

		Assert.Contains("class=\"mobile-shell\"", body);
		Assert.Contains("storefront-search", body);
		Assert.Equal("true", AccountNavAriaCurrent(body));
		Assert.Contains("/Identity/Account/Login", ExtractMobileNav(body));
		AssertMobileNavItems(body, "/Identity/Account/Login");
		Assert.DoesNotContain("Account/Logout", ExtractOffcanvas(body));
		Assert.Single(Regex.Matches(body, "data-bs-target=\"#sideNavbarTop\""));
	}

	[Fact]
	public async Task IdentityRegister_ShowsMobileShell()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient();

		var body = await client.GetStringAsync("/Identity/Account/Register");

		Assert.Contains("class=\"mobile-shell\"", body);
		AssertMobileNavItems(body, "/Identity/Account/Login");
		Assert.Single(Regex.Matches(body, "data-bs-target=\"#sideNavbarTop\""));
	}

	[Fact]
	public async Task IdentityLogin_KeepsDesktopTopNavbar()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient();

		var body = await client.GetStringAsync("/Identity/Account/Login");

		Assert.Contains("id=\"userDropdown\"", body);
		Assert.Contains("d-none d-lg-flex", body);
	}

	[Fact]
	public async Task ManageAccount_ShowsMobileShellAndLogoutReturnsHome()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

		var response = await client.GetAsync("/Identity/Account/Manage/Index");
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadAsStringAsync();

		Assert.Contains("class=\"mobile-shell\"", body);
		AssertMobileNavItems(body, "/Identity/Account/Manage");

		var form = ExtractManageLogoutForm(body);
		Assert.Contains("__RequestVerificationToken", form);
		Assert.Equal("/Identity/Account/Logout?returnUrl=%2F", FormAction(form));

		var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/Identity/Account/Manage/Index");
		var logout = await client.PostAsync(FormAction(form), new FormUrlEncodedContent(
			new Dictionary<string, string>
			{
				["__RequestVerificationToken"] = token,
			}));

		Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
		Assert.Equal("/", logout.Headers.Location!.OriginalString);

		var afterLogout = await client.GetAsync("/Identity/Account/Manage/Index");
		Assert.Equal(HttpStatusCode.Redirect, afterLogout.StatusCode);
		Assert.Equal("/Identity/Account/Login", afterLogout.Headers.Location!.AbsolutePath);
	}

	[Fact]
	public async Task SignedInStorefront_BottomBarLinksMyAccountToManage()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

		var body = await client.GetStringAsync("/en-US/Customer/Home/Index");

		Assert.Contains("/Identity/Account/Manage", ExtractMobileNav(body));

		var action = FormAction(ExtractOffcanvasLogoutForm(body));
		Assert.Contains("returnUrl=", action);
		var returnUrl = Uri.UnescapeDataString(action[(action.IndexOf("returnUrl=", StringComparison.Ordinal) + "returnUrl=".Length)..]);
		Assert.DoesNotContain("/Identity", returnUrl);

		var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Customer/Home/Index");
		var logout = await client.PostAsync(action, new FormUrlEncodedContent(
			new Dictionary<string, string>
			{
				["__RequestVerificationToken"] = token,
			}));

		Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
		Assert.Equal(returnUrl, logout.Headers.Location!.OriginalString);
	}

	[Fact]
	public async Task Admin_KeepsOperationalNavigationWithoutShopperBar()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);

		var body = await client.GetStringAsync("/en-US/Admin/Category/Index");

		Assert.DoesNotContain("storefront-mobile-nav", body);
		Assert.DoesNotContain("mobile-shell", body);
		Assert.Contains("data-bs-target=\"#sideNavbarTop\"", body);
		Assert.Contains("/Admin/Category", body);
		Assert.DoesNotContain("Account/Logout", ExtractOffcanvas(body));
	}

	private static string ExtractMobileNav(string body)
	{
		return ExtractBetween(body, "storefront-mobile-nav", "</nav>", "mobile bottom navigation");
	}

	private static string ExtractOffcanvas(string body)
	{
		return ExtractBetween(body, "id=\"sideNavbarTop\"", "</section>", "mobile menu offcanvas");
	}

	private static string ExtractOffcanvasLogoutForm(string body)
	{
		var match = Regex.Match(ExtractOffcanvas(body), "(?s)<form[^>]*action=\"[^\"]*Account/Logout[^\"]*\"[^>]*>.*?</form>");
		Assert.True(match.Success, "Offcanvas logout form not found.");
		return match.Value;
	}

	private static string ExtractBetween(string body, string startMarker, string endMarker, string label)
	{
		var start = body.IndexOf(startMarker, StringComparison.Ordinal);
		Assert.True(start >= 0, $"Expected a {label}.");
		var end = body.IndexOf(endMarker, start, StringComparison.Ordinal);
		Assert.True(end > start, $"The {label} is not closed.");
		return body.Substring(start, end - start);
	}

	private static void AssertMobileNavItems(string body, string accountHref)
	{
		var nav = ExtractMobileNav(body);
		Assert.Equal(5, Regex.Matches(nav, "storefront-mobile-nav__link").Count);
		Assert.Contains("bi-house-door", nav);
		Assert.Contains("data-bs-target=\"#storefrontSearch\"", nav);
		Assert.Contains("bi-cart3", nav);
		Assert.Contains(accountHref, nav);
		Assert.Contains("data-bs-target=\"#sideNavbarTop\"", nav);
	}

	private static string AccountNavAriaCurrent(string body)
	{
		var match = Regex.Match(ExtractMobileNav(body), "aria-current=\"([^\"]+)\"");
		Assert.True(match.Success, "My account entry is not marked as current.");
		return match.Groups[1].Value;
	}

	private static string ExtractManageLogoutForm(string body)
	{
		var match = Regex.Match(body, "(?s)<form[^>]*action=\"[^\"]*Account/Logout[^\"]*\"[^>]*>(?:(?!</form>).)*id=\"manage-logout\".*?</form>");
		Assert.True(match.Success, "Manage logout form not found.");
		return match.Value;
	}

	private static string FormAction(string form)
	{
		var match = Regex.Match(form, "action=\"([^\"]+)\"");
		Assert.True(match.Success, "Form action not found.");
		return match.Groups[1].Value;
	}

	[Theory]
	[InlineData("es-AR", "Iniciar Sesi")]
	[InlineData("en-US", "Log In")]
	public async Task IdentityLogin_HasSinglePageHeading(string culture, string expectedHeading)
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient();

		var body = await client.GetStringAsync($"/Identity/Account/Login?culture={culture}");

		Assert.Single(Regex.Matches(body, "<h1[ >]"));
		var heading = Regex.Match(body, "<h1[^>]*>(.*?)</h1>", RegexOptions.Singleline);
		Assert.True(heading.Success);
		Assert.Contains(expectedHeading, heading.Groups[1].Value);
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
