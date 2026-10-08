using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VaultShop.DataAccess.Data;
using VaultShop.Models;

namespace VaultShop.Web.Tests;

// P2.1: the badge/strikethrough markup itself, in both cultures. StorefrontPricingServiceTests
// only proved the data (HasBadge/Motive); this proves a shopper actually sees the badge and
// the struck-through original price.
public class StorefrontDiscountRenderingHttpTests
{
    [Theory]
    [InlineData("en-US", "Offer")]
    [InlineData("es-AR", "Oferta")]
    public async Task HomeIndex_OfferProduct_RendersBadgeAndStrikethrough(string culture, string offerLabel)
    {
        using var factory = new CustomWebApplicationFactory();
        Seed(factory, promo: false);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var body = await client.GetStringAsync($"/{culture}/Customer/Home/Index");

        Assert.Contains("Offer Product", body);
        Assert.Contains($"badge bg-success\">{offerLabel}</span>", body);
        Assert.Contains("<s class=\"text-muted fw-normal\">", body);
        // struck-through original, then the offer price
        Assert.Matches(@"<s class=""text-muted fw-normal"">.*</s> <span>.*</span>", body);
    }

    [Fact]
    public async Task HomeIndex_BxGyProduct_RendersPromotionBadgeWithoutStrikethrough()
    {
        using var factory = new CustomWebApplicationFactory();
        // no direct offer: the badge must come from the covering BxGy promotion
        Seed(factory, promo: true, offer: false);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/en-US/Customer/Home/Index");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("badge bg-success\">2x1</span>", body);
        // the discount lands in the cart, so no struck-through price on the card
        Assert.DoesNotContain("<s class=\"text-muted fw-normal\">", body);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("es-AR")]
    public async Task HomeIndex_NoDiscount_RendersNoBadgeAndNoStrikethrough(string culture)
    {
        using var factory = new CustomWebApplicationFactory();
        Seed(factory, promo: false, offer: false);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var body = await client.GetStringAsync($"/{culture}/Customer/Home/Index");

        Assert.Contains("Offer Product", body);
        Assert.DoesNotContain("badge bg-success", body);
        Assert.DoesNotContain("<s class=\"text-muted fw-normal\">", body);
    }

    private static void Seed(CustomWebApplicationFactory factory, bool promo, bool offer = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = new Category { Name = "Test Category", AvgShippingCost = 100m };
        db.Categories.Add(category);
        db.SaveChanges();

        db.Products.Add(new Product
        {
            Name = "Offer Product",
            Description = "Test",
            MaxExpectation = 10,
            Category = category,
            ListPrice = 100m,
            FinalRetailPrice = 100m,
            FinalWholesalePrice = 70m,
            SaleRetailPrice = offer ? 80m : null,
            SaleFromUtc = DateTime.UtcNow.AddDays(-1),
            SaleToUtc = DateTime.UtcNow.AddDays(1),
            IsAvailableInStore = true,
            IsDeleted = false,
            StockQuantity = 5,
        });
        db.SaveChanges();

        if (promo)
        {
            db.Promotions.Add(new Promotion
            {
                Name = "2x1",
                Kind = PromotionKind.BxGy,
                Scope = PromotionScope.Store,
                BuyQty = 1,
                GetQty = 1,
                GetDiscountPercent = 100m,
                IsActive = true
            });
            db.SaveChanges();
        }
    }
}