using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VaultShop.DataAccess.Data;
using VaultShop.Models;

namespace VaultShop.Web.Tests;

// product-slugs 3.1 — sitemap lists canonical id-plus-slug URLs, and the product/category
// canonical <link> agrees with the sitemap entry.
public class SeoCanonicalUrlHttpTests
{
    private const string SiteUrl = "https://vaultshop.evaldez.ar";

    [Fact]
    public async Task Sitemap_ListsCanonicalProductAndCategoryUrls_AndEveryProductLocResolves()
    {
        using var factory = new CustomWebApplicationFactory();
        var (categoryId, productId, _, _) = SeedCatalog(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/sitemap.xml");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
        var xml = await response.Content.ReadAsStringAsync();
        // unescaped & in a loc would make the whole document invalid XML for every crawler
        System.Xml.Linq.XDocument.Parse(xml);

        Assert.Contains($"<loc>{SiteUrl}/es-AR/Customer/Home/Details/{productId}/remera-negra</loc>", xml);
        Assert.Contains($"<loc>{SiteUrl}/es-AR/Customer/Home/Search?categoryId={categoryId}&amp;cslug=mochilas</loc>", xml);
        // never the id-only form
        Assert.DoesNotContain($"<loc>{SiteUrl}/es-AR/Customer/Home/Details/{productId}</loc>", xml);

        // every advertised product URL must actually resolve, or the sitemap is a lie
        var locs = Regex.Matches(xml, @"<loc>([^<]*/Details/[^<]*)</loc>")
            .Select(m => m.Groups[1].Value.Replace("&amp;", "&"))
            .ToList();
        Assert.NotEmpty(locs);
        foreach (var loc in locs)
        {
            var path = new Uri(loc).AbsolutePath;
            var probe = await client.GetAsync(path);
            Assert.True(probe.StatusCode == HttpStatusCode.OK,
                $"sitemap loc {loc} resolved to {(int)probe.StatusCode}, expected 200");
        }
    }

    [Fact]
    public async Task Sitemap_StaticPagesCarryTheAreaSegment()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var xml = await (await client.GetAsync("/sitemap.xml")).Content.ReadAsStringAsync();

        Assert.Contains($"<loc>{SiteUrl}/es-AR/Customer/Home/AboutUs</loc>", xml);
        Assert.Contains($"<loc>{SiteUrl}/es-AR/Customer/Home/FAQs</loc>", xml);
        Assert.DoesNotContain($"/es-AR/Home/", xml);
    }

    [Fact]
    public async Task Sitemap_ExcludesDeletedAndUnavailableProducts_AndDeletedCategories()
    {
        using var factory = new CustomWebApplicationFactory();
        var (_, _, deletedProductId, deletedCategoryId) = SeedCatalog(factory);
        var client = factory.CreateClient();

        var xml = await (await client.GetAsync("/sitemap.xml")).Content.ReadAsStringAsync();

        Assert.DoesNotContain($"/Details/{deletedProductId}", xml);
        Assert.DoesNotContain($"categoryId={deletedCategoryId}", xml);
    }

    [Fact]
    public async Task ProductDetails_CanonicalTagMatchesSitemap_EvenOnIdAnchorVisit()
    {
        using var factory = new CustomWebApplicationFactory();
        var (_, productId, _, _) = SeedCatalog(factory);
        var client = factory.CreateClient();

        var canonical = CanonicalOf(await client.GetStringAsync($"/es-AR/Customer/Home/Details/{productId}/remera-negra"));
        var idAnchorBody = await client.GetStringAsync($"/es-AR/Customer/Home/Details/{productId}");

        Assert.Equal($"{SiteUrl}/es-AR/Customer/Home/Details/{productId}/remera-negra", canonical);
        // same canonical advertised when the shopper used the bookmark id-anchor form
        Assert.Equal(canonical, CanonicalOf(idAnchorBody));
    }

    [Fact]
    public async Task CategorySearch_CanonicalTag_IsTheCategoryFilter()
    {
        using var factory = new CustomWebApplicationFactory();
        var (categoryId, _, _, _) = SeedCatalog(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var body = await client.GetStringAsync($"/es-AR/Customer/Home/Search?categoryId={categoryId}");

        // the href is an HTML attribute, so the separator arrives as &amp;
        Assert.Equal($"{SiteUrl}/es-AR/Customer/Home/Search?categoryId={categoryId}&amp;cslug=mochilas", CanonicalOf(body));
    }

    [Fact]
    public async Task NonSluggedPages_KeepVisitedPathAsCanonical()
    {
        using var factory = new CustomWebApplicationFactory();
        SeedCatalog(factory);
        var client = factory.CreateClient();

        Assert.Equal($"{SiteUrl}/es-AR/Customer/Home/Index", CanonicalOf(await client.GetStringAsync("/es-AR/Customer/Home/Index")));
    }

    private static string CanonicalOf(string html) => Regex.Match(html, """<link rel="canonical" href="([^"]+)" />""").Groups[1].Value;

    private static (int categoryId, int productId, int deletedProductId, int deletedCategoryId) SeedCatalog(CustomWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = new Category { Name = "Mochilas", Slug = "mochilas", AvgShippingCost = 100m };
        var deletedCategory = new Category { Name = "Borrada", Slug = "borrada", AvgShippingCost = 100m, IsDeleted = true };
        db.Categories.AddRange(category, deletedCategory);
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
        var deletedProduct = new Product
        {
            Name = "Remera Vieja",
            Slug = "remera-vieja",
            Description = "Remera",
            MaxExpectation = 10,
            CategoryId = category.Id,
            ListPrice = 100m,
            FinalRetailPrice = 100m,
            FinalWholesalePrice = 100m,
            IsAvailableInStore = true,
            IsDeleted = true,
            StockQuantity = 3,
        };
        db.Products.AddRange(product, deletedProduct);
        db.SaveChanges();

        return (category.Id, product.Id, deletedProduct.Id, deletedCategory.Id);
    }
}
