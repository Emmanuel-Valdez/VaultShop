using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VaultShop.DataAccess.Data;
using VaultShop.Models;

namespace VaultShop.Web.Tests;

public class SearchHttpTests
{
    [Theory]
    [InlineData("sauron")]
    [InlineData("Sauron")]
    [InlineData("SAURON")]
    [InlineData("Saurón")]
    public async Task SearchHttp_MatchesAreCaseAndAccentInsensitive(string searchString)
    {
        using var factory = new CustomWebApplicationFactory();
        SeedProduct(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var url = $"/en-US/Customer/Home/Search?searchString={Uri.EscapeDataString(searchString)}";
        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Sauron Amulet Bag", body);
    }

    [Fact]
    public async Task SearchHttp_WithNoMatches_RedirectsToIndex()
    {
        using var factory = new CustomWebApplicationFactory();
        SeedProduct(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/en-US/Customer/Home/Search?searchString=zzz-no-such-product-xyz");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        // RedirectToAction("Index") from Home controller matches route defaults ?
        // only non-default culture segment "en-US" is included in the Location.
        Assert.Contains("/en-US", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task HomeIndex_RendersCollectionRow_WhenKeywordAssociatedToVisibleProduct()
    {
        using var factory = new CustomWebApplicationFactory();
        SeedProductWithKeyword(factory);
        var client = factory.CreateClient();

        var body = await client.GetStringAsync("/en-US/Customer/Home/Index");

        Assert.Contains("Test Collection", body);
        Assert.Contains("collection-chip", body);
        Assert.Contains("keywordId=1", body);
    }

    [Fact]
    public async Task HomeIndex_OmitsCollectionSection_WhenNoKeywords()
    {
        using var factory = new CustomWebApplicationFactory();
        SeedProduct(factory);
        var client = factory.CreateClient();

        var body = await client.GetStringAsync("/en-US/Customer/Home/Index");

        Assert.DoesNotContain("collection-chip", body);
    }

    [Fact]
    public async Task SearchHttp_KeywordFiltersByExactId()
    {
        using var factory = new CustomWebApplicationFactory();
        var (categoryId, keywordId, _) = SeedCatalog(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var body = await client.GetStringAsync($"/en-US/Customer/Home/Search?keywordId={keywordId}");

        Assert.Contains("Mochila Negra", body);
        Assert.Contains("Mochila Azul", body);
        Assert.DoesNotContain("Mochila Sin Coleccion", body);
    }

    [Fact]
    public async Task SearchHttp_CombinedFilters_ReturnAndIntersection()
    {
        using var factory = new CustomWebApplicationFactory();
        var (categoryId, keywordId, _) = SeedCatalog(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var body = await client.GetStringAsync($"/en-US/Customer/Home/Search?keywordId={keywordId}&categoryId={categoryId}&searchString=negra");

        Assert.Contains("Mochila Negra", body);
        Assert.DoesNotContain("Mochila Azul", body);
        Assert.DoesNotContain("Mochila Negra Otra Categoria", body);
    }

    [Fact]
    public async Task SearchHttp_CategoryShortcut_FiltersByIdWithoutText()
    {
        using var factory = new CustomWebApplicationFactory();
        var (categoryId, _, _) = SeedCatalog(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var body = await client.GetStringAsync($"/en-US/Customer/Home/Search?categoryId={categoryId}");

        Assert.Contains("Mochila Negra", body);
        Assert.DoesNotContain("Mochila Negra Otra Categoria", body);
    }

    [Fact]
    public async Task SearchHttp_MissingKeywordId_RendersEmptyResultsInsteadOfRedirect()
    {
        using var factory = new CustomWebApplicationFactory();
        SeedProduct(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/en-US/Customer/Home/Search?keywordId=999");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No products matched your search", body);
    }

    [Fact]
    public async Task StorefrontCollections_EndToEndFlow()
    {
        using var factory = new CustomWebApplicationFactory();
        var (categoryId, keywordId) = SeedLargeCatalog(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var home = await client.GetStringAsync("/en-US/Customer/Home/Index");
        Assert.Contains("collection-chip", home);
        Assert.Contains("(13)", home);

        var filtered = await client.GetStringAsync($"/en-US/Customer/Home/Search?keywordId={keywordId}");
        Assert.Contains("collection-chip active", filtered);
        Assert.Contains("aria-current=\"true\"", filtered);

        var page2Match = Regex.Match(filtered, @"href=""([^""]*pageNumber=2[^""]*)""");
        Assert.True(page2Match.Success, "expected a page-2 pager link");
        Assert.Contains($"keywordId={keywordId}", page2Match.Groups[1].Value);
        Assert.DoesNotContain("categoryId=", page2Match.Groups[1].Value);

        var combined = await client.GetStringAsync($"/en-US/Customer/Home/Search?keywordId={keywordId}&categoryId={categoryId}&searchString=Mochila");
        Assert.Contains("active-filter-chip__label", combined);
        Assert.Contains("Mochilas", combined);
        Assert.Contains("Remove Mochilas filter", combined);
        Assert.Contains("Remove Naruto filter", combined);

        var removeIdx = combined.IndexOf("collection-chip__remove");
        Assert.True(removeIdx >= 0, "expected a remove-collection anchor");
        var hrefStart = combined.IndexOf("href=\"", removeIdx);
        var hrefEnd = combined.IndexOf("\"", hrefStart + 6);
        var removeHref = combined.Substring(hrefStart + 6, hrefEnd - (hrefStart + 6));
        Assert.Contains("categoryId=", removeHref);
        Assert.Contains("searchString=", removeHref);
        Assert.DoesNotContain("keywordId=", removeHref);
    }

    [Fact]
    public async Task Search_AdditiveFilters_Preserve()
    {
        using var factory = new CustomWebApplicationFactory();
        var (categoryId, keywordId, _) = SeedCatalog(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // from ?categoryId=X clicking collection Y → should keep categoryId and add slug
        var catFiltered = await client.GetStringAsync($"/en-US/Customer/Home/Search?categoryId={categoryId}");
        Assert.Contains("collection-chip__link", catFiltered);
        var chipHrefIdx = catFiltered.IndexOf("collection-chip__link");
        Assert.True(chipHrefIdx >= 0);
        // find first chip link href after that marker
        var hrefKeyword = catFiltered.IndexOf($"keywordId={keywordId}", chipHrefIdx);
        Assert.True(hrefKeyword >= 0, "collection chip should link to keywordId");
        var hrefCategory = catFiltered.IndexOf($"categoryId={categoryId}", chipHrefIdx);
        Assert.True(hrefCategory >= 0, "collection chip should preserve categoryId (AND navigation)");
        Assert.Contains("slug=naruto", catFiltered);

        // from ?keywordId=Y clicking category X → should keep keywordId+slug
        var collFiltered = await client.GetStringAsync($"/en-US/Customer/Home/Search?keywordId={keywordId}&slug=naruto");
        // category button for Mochilas should preserve keywordId+slug
        Assert.Contains($"keywordId={keywordId}", collFiltered);
        Assert.Contains("slug=naruto", collFiltered);
        // category chip href for our category should contain both
        var catBtnIdx = collFiltered.IndexOf($"categoryId={categoryId}");
        Assert.True(catBtnIdx >= 0, "category button should be present");
        // ensure the surrounding anchor also has keywordId
        var catAnchorStart = collFiltered.LastIndexOf("href=\"", catBtnIdx);
        var catAnchorEnd = collFiltered.IndexOf("\"", catBtnIdx);
        var catAnchor = collFiltered.Substring(catAnchorStart, catAnchorEnd - catAnchorStart);
        Assert.Contains($"keywordId={keywordId}", catAnchor);
        Assert.Contains("slug=naruto", catAnchor);

        // removing collection keeps category
        var combined = await client.GetStringAsync($"/en-US/Customer/Home/Search?keywordId={keywordId}&slug=naruto&categoryId={categoryId}&searchString=negra");
        var rIdx = combined.IndexOf("collection-chip__remove");
        Assert.True(rIdx >= 0, "remove collection link expected");
        var rhStart = combined.IndexOf("href=\"", rIdx);
        var rhEnd = combined.IndexOf("\"", rhStart + 6);
        var rHref = combined.Substring(rhStart + 6, rhEnd - (rhStart + 6));
        Assert.Contains($"categoryId={categoryId}", rHref);
        Assert.Contains("searchString=negra", rHref);
        Assert.DoesNotContain("keywordId=", rHref);
        Assert.DoesNotContain("slug=", rHref);

        // pager keeps slug (use large catalog to ensure paging)
        using var factoryLarge = new CustomWebApplicationFactory();
        var (catLarge, kwLarge) = SeedLargeCatalog(factoryLarge);
        var clientLarge = factoryLarge.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var largeBody = await clientLarge.GetStringAsync($"/en-US/Customer/Home/Search?keywordId={kwLarge}");
        var pageMatch = Regex.Match(largeBody, @"href=""([^""]*pageNumber=2[^""]*)""");
        Assert.True(pageMatch.Success, "expected pager page 2 link with slug preserved");
        Assert.Contains($"keywordId={kwLarge}", pageMatch.Groups[1].Value);
        Assert.Contains("slug=naruto", pageMatch.Groups[1].Value);

        // slug mismatch redirects to canonical, slug absent still 200
        var wrongResp = await client.GetAsync($"/en-US/Customer/Home/Search?keywordId={keywordId}&slug=wrong");
        Assert.Equal(HttpStatusCode.MovedPermanently, wrongResp.StatusCode);
        Assert.Contains("slug=naruto", wrongResp.Headers.Location!.OriginalString);
        var okNoSlug = await client.GetAsync($"/en-US/Customer/Home/Search?keywordId={keywordId}");
        Assert.Equal(HttpStatusCode.OK, okNoSlug.StatusCode);
        var okBody = await okNoSlug.Content.ReadAsStringAsync();
        Assert.Contains("naruto", okBody.ToLowerInvariant());
    }

    private static (int categoryId, int keywordId) SeedLargeCatalog(CustomWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var mochilas = new Category { Name = "Mochilas", AvgShippingCost = 100m };
        var ropa = new Category { Name = "Ropa", AvgShippingCost = 100m };
        var keyword = new Keyword { Name = "Naruto", Slug = "naruto", IsDeleted = false };
        db.Categories.AddRange(mochilas, ropa);
        db.Keywords.Add(keyword);
        db.SaveChanges();
        db.KeywordImages.Add(new KeywordImage
        {
            Kind = KeywordImageKind.Chip,
            ImageUrl = $"images/keywords/keyword-{keyword.Id}/chip.jpg",
            KeywordId = keyword.Id,
        });
        db.SaveChanges();

        var products = new List<Product>();
        for (var i = 0; i < 10; i++)
        {
            products.Add(new Product
            {
                Name = $"Mochila Naruto {i}",
                Description = "Mochila",
                MaxExpectation = 10,
                Category = mochilas,
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
                Description = "Remera",
                MaxExpectation = 10,
                Category = ropa,
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

    private static (int categoryId, int keywordId, int productId) SeedCatalog(CustomWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = new Category { Name = "Mochilas", AvgShippingCost = 100m };
        var otherCategory = new Category { Name = "Ropa", AvgShippingCost = 100m };
        var keyword = new Keyword { Name = "Naruto", Slug = "naruto", IsDeleted = false };
        db.Categories.AddRange(category, otherCategory);
        db.Keywords.Add(keyword);
        db.SaveChanges();

        var product = new Product
        {
            Name = "Mochila Negra",
            Description = "Mochila",
            MaxExpectation = 10,
            Category = category,
            ListPrice = 100m,
            FinalRetailPrice = 100m,
            FinalWholesalePrice = 100m,
            IsAvailableInStore = true,
            IsDeleted = false,
            StockQuantity = 3,
        };
        var taggedAzul = new Product
        {
            Name = "Mochila Azul",
            Description = "Mochila",
            MaxExpectation = 10,
            Category = category,
            ListPrice = 100m,
            FinalRetailPrice = 100m,
            FinalWholesalePrice = 100m,
            IsAvailableInStore = true,
            IsDeleted = false,
            StockQuantity = 3,
        };
        var otherCategoryNegra = new Product
        {
            Name = "Mochila Negra Otra Categoria",
            Description = "Mochila",
            MaxExpectation = 10,
            Category = otherCategory,
            ListPrice = 100m,
            FinalRetailPrice = 100m,
            FinalWholesalePrice = 100m,
            IsAvailableInStore = true,
            IsDeleted = false,
            StockQuantity = 3,
        };
        var untagged = new Product
        {
            Name = "Mochila Sin Coleccion",
            Description = "Mochila",
            MaxExpectation = 10,
            Category = category,
            ListPrice = 100m,
            FinalRetailPrice = 100m,
            FinalWholesalePrice = 100m,
            IsAvailableInStore = true,
            IsDeleted = false,
            StockQuantity = 3,
        };
        db.Products.AddRange(product, taggedAzul, otherCategoryNegra, untagged);
        db.SaveChanges();
        db.ProductKeywords.AddRange(
            new ProductKeyword { ProductId = product.Id, KeywordId = keyword.Id },
            new ProductKeyword { ProductId = taggedAzul.Id, KeywordId = keyword.Id },
            new ProductKeyword { ProductId = otherCategoryNegra.Id, KeywordId = keyword.Id });
        db.SaveChanges();

        return (category.Id, keyword.Id, product.Id);
    }

    private static void SeedProductWithKeyword(CustomWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = new Category { Name = "Test Category", AvgShippingCost = 100m };
        var keyword = new Keyword { Name = "Test Collection", Slug = "test-collection", IsDeleted = false };
        var product = new Product
        {
            Name = "Sauron Amulet Bag",
            Description = "Ojo de Saurón",
            MaxExpectation = 10,
            Category = category,
            ListPrice = 100m,
            FinalRetailPrice = 100m,
            FinalWholesalePrice = 100m,
            IsAvailableInStore = true,
            IsDeleted = false,
            StockQuantity = 3,
        };
        db.Categories.Add(category);
        db.Keywords.Add(keyword);
        db.Products.Add(product);
        db.SaveChanges();
        db.ProductKeywords.Add(new ProductKeyword { ProductId = product.Id, KeywordId = keyword.Id });
        db.SaveChanges();
    }

    private static void SeedProduct(CustomWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = new Category
        {
            Name = "Test Category",
            AvgShippingCost = 100m,
        };
        var product = new Product
        {
            Name = "Sauron Amulet Bag",
            Description = "Ojo de Saurón",
            MaxExpectation = 10,
            Category = category,
            ListPrice = 100m,
            FinalRetailPrice = 100m,
            FinalWholesalePrice = 100m,
            IsAvailableInStore = true,
            IsDeleted = false,
        };
        db.Categories.Add(category);
        db.Products.Add(product);
        db.SaveChanges();
    }
}