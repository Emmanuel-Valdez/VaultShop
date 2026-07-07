using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VaultShop.DataAccess.Data;
using VaultShop.Models;
using VaultShop.Utility;

namespace VaultShop.Web.Tests;

public class CartCheckoutHttpTests
{
    [Fact]
    public async Task SummaryPost_AsCustomer_CreatesPendingOrderAndRedirectsToStripeSession()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        SeedProductAndCart(factory, factory.CustomerEmail, count: 2, retailPrice: 100m, wholesalePrice: 70m);

        await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

        var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Customer/Cart/Summary");

        var response = await PostSummary(client, token);

        Assert.Equal((HttpStatusCode)303, response.StatusCode);
        Assert.Equal("https://stripe.test/checkout", response.Headers.Location!.ToString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var orderHeader = Assert.Single(db.OrderHeaders.AsNoTracking());
        Assert.Equal(SD.PaymentStatusPending, orderHeader.PaymentStatus);
        Assert.Equal(SD.StatusPending, orderHeader.OrderStatus);
        Assert.Equal(200m, orderHeader.OrderTotal);
        Assert.Equal("cs_test_123", orderHeader.SessionId);
        Assert.Equal("pi_test_123", orderHeader.PaymentIntentId);
    }

    [Fact]
    public async Task SummaryPost_AsCompany_CreatesApprovedDelayedOrderAndSkipsStripe()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        SeedProductAndCart(factory, factory.CompanyEmail, count: 3, retailPrice: 100m, wholesalePrice: 70m);

        await TestAuthHelper.LoginAsync(client, factory.CompanyEmail, factory.TestPassword);

        var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Customer/Cart/Summary");

        var response = await PostSummary(client, token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Cart/OrderConfirmation", response.Headers.Location!.ToString());
        Assert.DoesNotContain("stripe", response.Headers.Location!.ToString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var orderHeader = Assert.Single(db.OrderHeaders.AsNoTracking());
        Assert.Equal(SD.PaymentStatusDelayedPayment, orderHeader.PaymentStatus);
        Assert.Equal(SD.StatusApproved, orderHeader.OrderStatus);
        Assert.Equal(factory.TestCompanyId, orderHeader.CompanyId);
        Assert.Equal(210m, orderHeader.OrderTotal);
        Assert.Equal(DateOnly.FromDateTime(orderHeader.OrderDate.AddDays(SD.CompanyPaymentDueDays)), orderHeader.PaymentDueDate);
    }

    [Fact]
    public async Task SummaryPost_AsCustomer_WithBankTransfer_CreatesPendingOrderWithoutStripeSession()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        SeedProductAndCart(factory, factory.CustomerEmail, count: 2, retailPrice: 100m, wholesalePrice: 70m);

        await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

        var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Customer/Cart/Summary");

        var response = await PostSummary(client, token, SD.PaymentMethodBankTransfer);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Cart/OrderConfirmation", response.Headers.Location!.ToString());
        Assert.DoesNotContain("stripe", response.Headers.Location!.ToString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var orderHeader = Assert.Single(db.OrderHeaders.AsNoTracking());
        Assert.Equal(SD.PaymentMethodBankTransfer, orderHeader.PaymentMethod);
        Assert.Equal(SD.PaymentStatusPending, orderHeader.PaymentStatus);
        Assert.Equal(SD.StatusPending, orderHeader.OrderStatus);
        Assert.Null(orderHeader.SessionId);
    }

    [Fact]
    public async Task SummaryPost_WithEmptyCart_RedirectsToIndexWithoutCreatingOrder()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // No ShoppingCart rows seeded for this user
        await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

        // Summary GET would redirect (empty cart), so get token from authenticated Cart Index page instead
        var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Customer/Cart/Index");

        var response = await PostSummary(client, token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Customer/Cart", response.Headers.Location!.ToString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(db.OrderHeaders.AsNoTracking());
    }

    [Fact]
    public async Task SummaryPost_Unauthenticated_ChallengesToLogin()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // No login � empty POST, no token needed (auth challenge fires before antiforgery)
        var response = await client.PostAsync(
            "/en-US/Customer/Cart/Summary",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        var location = response.Headers.Location!;
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Identity/Account/Login", location.AbsolutePath);
        Assert.Contains("?ReturnUrl=", location.Query);
    }

    private static async Task<HttpResponseMessage> PostSummary(HttpClient client, string token, string paymentMethod = SD.PaymentMethodStripe)
    {
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["OrderHeader.Name"] = "Test Customer",
            ["OrderHeader.PhoneNumber"] = "555-0100",
            ["OrderHeader.StreetAddress"] = "123 Test St",
            ["OrderHeader.City"] = "Buenos Aires",
            ["OrderHeader.State"] = "Buenos Aires",
            ["OrderHeader.PostalCode"] = "1000",
            ["OrderHeader.PaymentMethod"] = paymentMethod,
        };
        return await client.PostAsync("/en-US/Customer/Cart/Summary", new FormUrlEncodedContent(form));
    }

    private static void SeedProductAndCart(CustomWebApplicationFactory factory, string email, int count, decimal retailPrice, decimal wholesalePrice)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = db.ApplicationUsers.Single(u => u.Email == email);

        var category = new Category
        {
            Name = "Test Category",
            MaxExpectation = 10,
            AvgShippingCost = 100m,
        };
        var product = new Product
        {
            Name = "Test Product",
            Description = "Product for HTTP checkout tests.",
            Category = category,
            ListPrice = retailPrice,
            FinalRetailPrice = retailPrice,
            FinalWholesalePrice = wholesalePrice,
            IsAvailableInStore = true,
            IsDeleted = false,
        };
        db.Categories.Add(category);
        db.Products.Add(product);
        db.ShoppingCarts.Add(new ShoppingCart
        {
            ApplicationUserId = user.Id,
            Product = product,
            Count = count,
        });
        db.SaveChanges();
    }
}