using System.Net;
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

    private static void SeedProduct(CustomWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = new Category
        {
            Name = "Test Category",
            MaxExpectation = 10,
            AvgShippingCost = 100m,
        };
        var product = new Product
        {
            Name = "Sauron Amulet Bag",
            Description = "Ojo de Saurón",
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