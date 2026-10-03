using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VaultShop.DataAccess.Data;
using VaultShop.Models;

namespace VaultShop.Web.Tests;

// product-slugs 2.1/2.2 — canonical /Details/{id}/{slug} URLs, mismatch-301, absent-slug-200,
// and no bare id-anchor links left in the rendered storefront markup.
public class ProductSlugUrlHttpTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("es-AR")]
    public async Task Details_CorrectSlug_Renders200(string culture)
    {
        using var factory = new CustomWebApplicationFactory();
        var (_, _, productId) = SeedCatalog(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/{culture}/Customer/Home/Details/{productId}/remera-negra");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Remera Negra", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("es-AR")]
    public async Task Details_WrongSlug_Redirects301ToCanonicalPreservingCulture(string culture)
    {
        using var factory = new CustomWebApplicationFactory();
        var (_, _, productId) = SeedCatalog(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/{culture}/Customer/Home/Details/{productId}/wrong-slug");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.Contains($"/{culture}/", location);
        Assert.Contains($"/Details/{productId}/remera-negra", location);
    }

    [Fact]
    public async Task Details_MissingSlug_Renders200WithoutRedirect()
    {
        using var factory = new CustomWebApplicationFactory();
        var (_, _, productId) = SeedCatalog(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/en-US/Customer/Home/Details/{productId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Remera Negra", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Details_UnknownId_Is404()
    {
        using var factory = new CustomWebApplicationFactory();
        SeedCatalog(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/en-US/Customer/Home/Details/99999/anything");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Details_LegacyQueryForm_StillRenders200()
    {
        using var factory = new CustomWebApplicationFactory();
        var (_, _, productId) = SeedCatalog(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/en-US/Customer/Home/Details?productId={productId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Remera Negra", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Search_WrongCslug_Redirects301ToCanonical_AndAbsentCslugStays200()
    {
        using var factory = new CustomWebApplicationFactory();
        var (categoryId, _, _) = SeedCatalog(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var wrong = await client.GetAsync($"/en-US/Customer/Home/Search?categoryId={categoryId}&cslug=mochilas-viejas");
        Assert.Equal(HttpStatusCode.MovedPermanently, wrong.StatusCode);
        Assert.Contains("cslug=mochilas", wrong.Headers.Location!.OriginalString);

        var ok = await client.GetAsync($"/en-US/Customer/Home/Search?categoryId={categoryId}&cslug=mochilas");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Contains("Remera Negra", await ok.Content.ReadAsStringAsync());

        var absent = await client.GetAsync($"/en-US/Customer/Home/Search?categoryId={categoryId}");
        Assert.Equal(HttpStatusCode.OK, absent.StatusCode);
    }

    // 2.2 — every storefront product link carries the slug; no bare id-anchor form survives.
    [Theory]
    [InlineData("/en-US/Customer/Home/Index")]
    [InlineData("/en-US/Customer/Home/Search?searchString=Mochila")]
    public async Task StorefrontLinks_CarrySlug_AndNeverBareIdAnchor(string url)
    {
        using var factory = new CustomWebApplicationFactory();
        var (_, _, productId) = SeedCatalog(factory);
        var client = factory.CreateClient();

        var body = System.Net.WebUtility.HtmlDecode(await client.GetStringAsync(url));

        Assert.Contains($"/Customer/Home/Details/{productId}/remera-negra", body);
        Assert.DoesNotContain("/Customer/Home/Details?productId=", body);
        Assert.DoesNotMatch(@"/Customer/Home/Details/" + productId + @"(?![/""?\d])", body);
    }

    [Fact]
    public async Task Search_CategoryChips_CarryCslug_AndPagerKeepsBothSlugs()
    {
        using var factory = new CustomWebApplicationFactory();
        var (categoryId, keywordId) = SeedLargeCatalog(factory);
        var client = factory.CreateClient();

        var raw = await client.GetStringAsync($"/en-US/Customer/Home/Search?categoryId={categoryId}&keywordId={keywordId}");
        var body = System.Net.WebUtility.HtmlDecode(raw);

        // pick the chip by its whole href — the <head> canonical link carries cslug too (no keywordId)
        var chipHref = Regex.Matches(body, "href=\"([^\"]*)\"")
            .Select(m => m.Groups[1].Value)
            .FirstOrDefault(h => h.Contains("cslug=mochilas") && h.Contains($"keywordId={keywordId}"));
        Assert.True(chipHref != null, "category chip should carry its own cslug alongside the active collection");
        Assert.Contains($"categoryId={categoryId}", chipHref);
        Assert.Contains("slug=naruto", chipHref);

        var page2 = Regex.Match(body, @"href=""([^""]*pageNumber=2[^""]*)""");
        Assert.True(page2.Success, "expected a page-2 pager link");
        Assert.Contains("slug=naruto", System.Net.WebUtility.HtmlDecode(page2.Groups[1].Value));
        Assert.Contains("cslug=mochilas", System.Net.WebUtility.HtmlDecode(page2.Groups[1].Value));
    }

    private static (int categoryId, int keywordId, int productId) SeedCatalog(CustomWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = new Category { Name = "Mochilas", Slug = "mochilas", AvgShippingCost = 100m };
        db.Categories.Add(category);
        db.SaveChanges();

        var product = new Product
        {
            Name = "Remera Negra",
            Slug = "remera-negra",
            Description = "Remera",
            MaxExpectation = 10,
            CategoryId = category.Id,
            ListPrice = 100m,
            FinalRetailPrice = 100m,
            FinalWholesalePrice = 100m,
            IsAvailableInStore = true,
            IsDeleted = false,
            StockQuantity = 3,
        };
        db.Products.Add(product);
        db.SaveChanges();

        return (category.Id, 0, product.Id);
    }

    private static (int categoryId, int keywordId) SeedLargeCatalog(CustomWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var mochilas = new Category { Name = "Mochilas", Slug = "mochilas", AvgShippingCost = 100m };
        var ropa = new Category { Name = "Ropa", Slug = "ropa", AvgShippingCost = 100m };
        var keyword = new Keyword { Name = "Naruto", Slug = "naruto", IsDeleted = false };
        db.Categories.AddRange(mochilas, ropa);
        db.Keywords.Add(keyword);
        db.SaveChanges();

        var products = new List<Product>();
        for (var i = 0; i < 20; i++)
        {
            products.Add(new Product
            {
                Name = $"Mochila Naruto {i}",
                Slug = $"mochila-naruto-{i}",
                Description = "Mochila",
                MaxExpectation = 10,
                CategoryId = mochilas.Id,
                ListPrice = 100m,
                FinalRetailPrice = 100m,
                FinalWholesalePrice = 100m,
                IsAvailableInStore = true,
                IsDeleted = false,
                StockQuantity = 3,
            });
        }
        for (var i = 0; i < 3; i++)
        {
            products.Add(new Product
            {
                Name = $"Remera Naruto {i}",
                Slug = $"remera-naruto-{i}",
                Description = "Remera",
                MaxExpectation = 10,
                CategoryId = ropa.Id,
                ListPrice = 100m,
                FinalRetailPrice = 100m,
                FinalWholesalePrice = 100m,
                IsAvailableInStore = true,
                IsDeleted = false,
                StockQuantity = 3,
            });
        }
        db.Products.AddRange(products);
        db.SaveChanges();
        foreach (var product in products)
        {
            db.ProductKeywords.Add(new ProductKeyword { ProductId = product.Id, KeywordId = keyword.Id });
        }
        db.SaveChanges();

        return (mochilas.Id, keyword.Id);
    }
}
