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