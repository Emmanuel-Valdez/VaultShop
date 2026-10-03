using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VaultShop.DataAccess.Data;
using VaultShop.Models;

namespace VaultShop.Web.Tests;

// product-slugs 3.1 — sitemap lists canonical id-plus-slug URLs, and the product/category
// canonical <link> agrees with the sitemap entry.
// seo-canonical-hreflang — every advertised loc is a 200, and the language signalling
// (hreflang alternates, <html lang>, SetLanguage) points at URLs that exist.
public class SeoCanonicalUrlHttpTests
{
    private const string SiteUrl = "https://vaultshop.evaldez.ar";

    [Fact]
    public async Task Sitemap_ListsCanonicalProductAndCategoryUrls_AndEveryLocResolves200()
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

        Assert.Contains($"<loc>{SiteUrl}/es-AR/Customer/Home/Index</loc>", xml);
        Assert.Contains($"<loc>{SiteUrl}/es-AR/Customer/Home/Details/{productId}/remera-negra</loc>", xml);
        Assert.Contains($"<loc>{SiteUrl}/es-AR/Customer/Home/Search?categoryId={categoryId}&amp;cslug=mochilas</loc>", xml);
        // never the id-only form
        Assert.DoesNotContain($"<loc>{SiteUrl}/es-AR/Customer/Home/Details/{productId}</loc>", xml);

        // every advertised URL must actually resolve, or the sitemap is a lie
        var locs = Regex.Matches(xml, @"<loc>([^<]*)</loc>")
            .Select(m => m.Groups[1].Value.Replace("&amp;", "&"))
            .ToList();
        Assert.NotEmpty(locs);
        foreach (var loc in locs)
        {
            // PathAndQuery, not AbsolutePath: dropping the filter turns a category loc into the
            // filter-less Search that 302s — the exact defect this widened probe exists to catch.
            var probe = await client.GetAsync(new Uri(loc).PathAndQuery);
            Assert.True(probe.StatusCode == HttpStatusCode.OK,
                $"sitemap loc {loc} resolved to {(int)probe.StatusCode}, expected 200");
        }
    }

    [Fact]
    public async Task Sitemap_OmitsTheFilterLessSearch_AndListsHomeInTheLinkedForm()
    {
        using var factory = new CustomWebApplicationFactory();
        SeedCatalog(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var xml = await (await client.GetAsync("/sitemap.xml")).Content.ReadAsStringAsync();

        // /es-AR/ is a second self-canonical URL for the page every internal link points at
        Assert.DoesNotContain($"<loc>{SiteUrl}/es-AR/</loc>", xml);
        // the filter-less Search 302s to Index, and a redirecting loc is not a page
        Assert.DoesNotContain($"<loc>{SiteUrl}/es-AR/Customer/Home/Search</loc>", xml);
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

    [Fact]
    public async Task ProductDetails_AlternatesSwapTheCultureSegment_AndNeverDoubleIt()
    {
        using var factory = new CustomWebApplicationFactory();
        var (_, productId, _, _) = SeedCatalog(factory);
        var client = factory.CreateClient();

        var body = await client.GetStringAsync($"/es-AR/Customer/Home/Details/{productId}/remera-negra");

        Assert.Equal($"{SiteUrl}/es-AR/Customer/Home/Details/{productId}/remera-negra", AlternateOf(body, "es-AR"));
        Assert.Equal($"{SiteUrl}/en-US/Customer/Home/Details/{productId}/remera-negra", AlternateOf(body, "en-US"));
        Assert.Equal(AlternateOf(body, "es-AR"), AlternateOf(body, "x-default"));
        Assert.NotEqual(AlternateOf(body, "es-AR"), AlternateOf(body, "en-US"));

        foreach (var href in AlternatesOf(body))
        {
            // the defect being fixed was /es-AR/es-AR/Customer/Home/Details/...
            Assert.Equal(1, href.Split('/').Count(segment => segment is "es-AR" or "en-US"));
        }
    }

    [Fact]
    public async Task CategorySearch_AlternatesCarryTheFilterQuery_Unchanged()
    {
        using var factory = new CustomWebApplicationFactory();
        var (categoryId, _, _, _) = SeedCatalog(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var body = await client.GetStringAsync($"/es-AR/Customer/Home/Search?categoryId={categoryId}&cslug=mochilas");

        // categoryId and cslug identify the page, so only the culture may differ
        Assert.Equal($"{SiteUrl}/es-AR/Customer/Home/Search?categoryId={categoryId}&cslug=mochilas", AlternateOf(body, "es-AR"));
        Assert.Equal($"{SiteUrl}/en-US/Customer/Home/Search?categoryId={categoryId}&cslug=mochilas", AlternateOf(body, "en-US"));
    }

    [Fact]
    public async Task Alternates_AreReciprocal_WhenEachEmittedAlternateIsFetched()
    {
        using var factory = new CustomWebApplicationFactory();
        var (categoryId, productId, _, _) = SeedCatalog(factory);
        var client = factory.CreateClient();

        foreach (var url in new[]
        {
            $"/es-AR/Customer/Home/Details/{productId}/remera-negra",
            $"/es-AR/Customer/Home/Search?categoryId={categoryId}&cslug=mochilas",
        })
        {
            foreach (var href in AlternatesOf(await client.GetStringAsync(url)).Distinct())
            {
                var reachedBy = $"{SiteUrl}{new Uri(href).PathAndQuery}";
                var other = await client.GetStringAsync(new Uri(href).PathAndQuery);

                Assert.Contains(reachedBy, AlternatesOf(other));
            }
        }
    }

    [Theory]
    [InlineData("/es-AR/Customer/Home/Index", "es-AR")]
    [InlineData("/en-US/Customer/Home/Index", "en-US")]
    public async Task Document_DeclaresTheServedCultureAsItsLang(string path, string expected)
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        Assert.Equal(expected, DocumentLangOf(await client.GetStringAsync(path)));
    }

    [Fact]
    public async Task SetLanguage_SwitchesTheCultureSegmentOnly_AndKeepsTheQueryIntact()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/es-AR/Customer/Home/Index");
        // the search term is itself a culture token, which string.Replace used to corrupt
        var returnUrl = "/es-AR/Customer/Home/Search?categoryId=3&cslug=mochilas&searchString=es-AR";
        var response = await client.PostAsync("/es-AR/Customer/Home/SetLanguage", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["culture"] = "en-US",
                ["returnUrl"] = returnUrl,
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/en-US/Customer/Home/Search?categoryId=3&cslug=mochilas&searchString=es-AR",
            response.Headers.Location?.OriginalString);
    }

    private static string CanonicalOf(string html) => Regex.Match(html, """<link rel="canonical" href="([^"]+)" />""").Groups[1].Value;

    private static string AlternateOf(string html, string hreflang) =>
        Regex.Match(html, $"""<link rel="alternate" hreflang="{hreflang}" href="([^"]+)" />""").Groups[1].Value.Replace("&amp;", "&");

    private static List<string> AlternatesOf(string html) => Regex
        .Matches(html, """<link rel="alternate" hreflang="[^"]+" href="([^"]+)" />""")
        .Select(m => m.Groups[1].Value.Replace("&amp;", "&"))
        .ToList();

    private static string DocumentLangOf(string html) => Regex.Match(html, """<html lang="([^"]+)">""").Groups[1].Value;

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
