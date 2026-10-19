using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VaultShop.DataAccess.Data;
using VaultShop.Models;

namespace VaultShop.Web.Tests;

// envio-gratis-o-cargo 5.2: the retail-only free-shipping badge, proven on each storefront surface
// and absent for wholesale viewers (Company users and admin wholesale preview).
public class FreeShippingBadgeHttpTests
{
    [Theory]
    [InlineData("en-US", "Free shipping")]
    [InlineData("es-AR", "gratis")]
    public async Task Home_Search_Details_RenderBadgeForRetailViewer(string culture, string labelFragment)
    {
        using var factory = new CustomWebApplicationFactory();
        var productId = SeedFreeProduct(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var home = await client.GetStringAsync($"/{culture}/Customer/Home/Index");
        var search = await client.GetStringAsync($"/{culture}/Customer/Home/Search?searchString={Uri.EscapeDataString("Free Ship Product")}");
        var details = await client.GetStringAsync($"/{culture}/Customer/Home/Details/{productId}");

        Assert.Contains("product-card__shipping-badge", home);
        Assert.Contains(labelFragment, home);
        Assert.Contains(labelFragment, search);
        Assert.Contains(labelFragment, details);
    }

    [Fact]
    public async Task Favorites_RenderBadgeForRetailViewer()
    {
        using var factory = new CustomWebApplicationFactory();
        var productId = SeedFreeProduct(factory);
        SeedFavorite(factory, factory.CustomerEmail, productId);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);
        var favorites = await client.GetStringAsync("/en-US/Customer/Favorite/Index");

        Assert.Contains("Free shipping", favorites);
    }

    [Fact]
    public async Task Home_CompanyViewer_HidesBadge()
    {
        using var factory = new CustomWebApplicationFactory();
        SeedFreeProduct(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await TestAuthHelper.LoginAsync(client, factory.CompanyEmail, factory.TestPassword);
        var html = await client.GetStringAsync("/en-US/Customer/Home/Index");

        Assert.DoesNotContain("Free shipping", html);
    }

    [Fact]
    public async Task Home_AdminWholesalePreview_HidesBadge()
    {
        using var factory = new CustomWebApplicationFactory();
        SeedFreeProduct(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);
        var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Customer/Home/Index");
        var response = await client.PostAsync("/en-US/Customer/Home/SetPreviewMode", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["mode"] = "wholesale",
            ["returnUrl"] = "/en-US/Customer/Home/Index",
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var html = await client.GetStringAsync("/en-US/Customer/Home/Index");
        Assert.DoesNotContain("Free shipping", html);
    }

    private static int SeedFreeProduct(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = new Category { Name = "Free Ship Category", AvgShippingCost = 100m };
        db.Categories.Add(category);
        db.SaveChanges();

        var product = new Product
        {
            Name = "Free Ship Product",
            Description = "Test",
            MaxExpectation = 10,
            Category = category,
            ListPrice = 100m,
            FinalRetailPrice = 100m,
            FinalWholesalePrice = 70m,
            FreeShipping = true,
            IsAvailableInStore = true,
            IsDeleted = false,
            StockQuantity = 5,
        };
        db.Products.Add(product);
        db.SaveChanges();
        return product.Id;
    }

    private static void SeedFavorite(WebApplicationFactory<Program> factory, string email, int productId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = db.ApplicationUsers.Single(u => u.Email == email);
        db.FavoriteProducts.Add(new FavoriteProduct { ProductId = productId, ApplicationUserId = user.Id });
        db.SaveChanges();
    }
}
