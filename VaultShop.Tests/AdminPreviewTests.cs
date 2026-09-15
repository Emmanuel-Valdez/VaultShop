using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VaultShop.DataAccess.Data;
using VaultShop.Models;
using VaultShop.Utility;
using VaultShop.Web.Services.Pricing;

namespace VaultShop.Web.Tests;

public class AdminPreviewTests
{
    // Unit: PricingHelper
    [Fact]
    public void ShouldUseWholesale_CompanyUser_ReturnsTrueRegardlessOfSession()
    {
        var product = new Product { FinalRetailPrice = 100m, FinalWholesalePrice = 70m };
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, SD.Role_Company)], "test"));
        var ctx = new DefaultHttpContext();
        ctx.Session = new FakeSession(); // empty
        ctx.User = user;

        Assert.True(PricingHelper.ShouldUseWholesale(user, ctx));
        Assert.Equal(70m, PricingHelper.GetPrice(product, user, ctx));
    }

    [Fact]
    public void ShouldUseWholesale_AdminWithoutPreview_ReturnsFalse()
    {
        var product = new Product { FinalRetailPrice = 100m, FinalWholesalePrice = 70m };
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, SD.Role_Admin)], "test"));
        var ctx = new DefaultHttpContext { Session = new FakeSession() };
        ctx.User = user;

        Assert.False(PricingHelper.ShouldUseWholesale(user, ctx));
        Assert.Equal(100m, PricingHelper.GetPrice(product, user, ctx));
    }

    [Fact]
    public void ShouldUseWholesale_AdminWithWholesalePreview_ReturnsTrue()
    {
        var product = new Product { FinalRetailPrice = 100m, FinalWholesalePrice = 70m };
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, SD.Role_Admin)], "test"));
        var session = new FakeSession();
        session.SetString(SD.AdminPreviewMode, "wholesale");
        var ctx = new DefaultHttpContext { Session = session };
        ctx.User = user;

        Assert.True(PricingHelper.IsWholesalePreview(ctx));
        Assert.True(PricingHelper.ShouldUseWholesale(user, ctx));
        Assert.Equal(70m, PricingHelper.GetPrice(product, user, ctx));
    }

    [Fact]
    public void ShouldUseWholesale_EmployeeWithWholesalePreview_ReturnsTrue()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, SD.Role_Employee)], "test"));
        var session = new FakeSession();
        session.SetString(SD.AdminPreviewMode, "wholesale");
        var ctx = new DefaultHttpContext { Session = session };
        ctx.User = user;

        Assert.True(PricingHelper.IsWholesalePreview(ctx));
        Assert.True(PricingHelper.ShouldUseWholesale(user, ctx));
    }

    [Fact]
    public void ShouldUseWholesale_CustomerWithWholesaleSession_ReturnsFalse()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, SD.Role_Customer)], "test"));
        var session = new FakeSession();
        session.SetString(SD.AdminPreviewMode, "wholesale");
        var ctx = new DefaultHttpContext { Session = session };
        ctx.User = user;

        Assert.False(PricingHelper.IsWholesalePreview(ctx));
        Assert.False(PricingHelper.ShouldUseWholesale(user, ctx));
    }

    [Fact]
    public void ShouldUseWholesale_AdminWithRetailPreview_ReturnsFalse()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, SD.Role_Admin)], "test"));
        var session = new FakeSession();
        session.SetString(SD.AdminPreviewMode, "retail");
        var ctx = new DefaultHttpContext { Session = session };
        ctx.User = user;

        Assert.False(PricingHelper.IsWholesalePreview(ctx));
        Assert.False(PricingHelper.ShouldUseWholesale(user, ctx));
    }

    // Integration: SetPreviewMode and banner + cart pricing
    [Fact]
    public async Task SetPreviewMode_AsCustomer_IsRejectedAndPriceRemainsRetail()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        SeedProduct(factory, retail: 100m, wholesale: 70m);
        await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

        var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Customer/Home/Index");
        var resp = await client.PostAsync("/en-US/Customer/Home/SetPreviewMode", new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["__RequestVerificationToken"] = token,
            ["mode"] = "wholesale",
            ["returnUrl"] = "/en-US/Customer/Home/Index"
        }));

        Assert.NotEqual(HttpStatusCode.OK, resp.StatusCode);
        var html = await client.GetStringAsync("/en-US/Customer/Home/Index");
        var wholesale = 70m.ToString("c", new System.Globalization.CultureInfo("en-US"));
        var retail = 100m.ToString("c", new System.Globalization.CultureInfo("en-US"));
        Assert.DoesNotContain(wholesale, html);
        Assert.Contains(retail, html);
    }

    [Fact]
    public async Task SetPreviewMode_AsAdmin_SwitchesPriceAndShowsBanner()
    {
        using var factory = new CustomWebApplicationFactory();
        var adminEmail = await SeedAdminAsync(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        SeedProduct(factory, retail: 100m, wholesale: 70m);
        await TestAuthHelper.LoginAsync(client, adminEmail, factory.TestPassword);

        var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Customer/Home/Index");
        var resp = await client.PostAsync("/en-US/Customer/Home/SetPreviewMode", new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["__RequestVerificationToken"] = token,
            ["mode"] = "wholesale",
            ["returnUrl"] = "/en-US/Customer/Home/Index"
        }));
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);

        var html = await client.GetStringAsync("/en-US/Customer/Home/Index");
        var wholesale = 70m.ToString("c", new System.Globalization.CultureInfo("en-US"));
        Assert.Contains(wholesale, html);
        Assert.Contains("Preview: wholesale pricing", html);
        Assert.Contains("role=\"status\"", html);
    }

    [Fact]
    public async Task SetPreviewMode_AdminRetail_ShowsRetailAndNoBanner()
    {
        using var factory = new CustomWebApplicationFactory();
        var adminEmail = await SeedAdminAsync(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        SeedProduct(factory, retail: 100m, wholesale: 70m);
        await TestAuthHelper.LoginAsync(client, adminEmail, factory.TestPassword);

        var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Customer/Home/Index");
        await client.PostAsync("/en-US/Customer/Home/SetPreviewMode", new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["__RequestVerificationToken"] = token,
            ["mode"] = "wholesale",
            ["returnUrl"] = "/en-US/Customer/Home/Index"
        }));
        // switch back
        token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Customer/Home/Index");
        await client.PostAsync("/en-US/Customer/Home/SetPreviewMode", new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["__RequestVerificationToken"] = token,
            ["mode"] = "retail",
            ["returnUrl"] = "/en-US/Customer/Home/Index"
        }));
        var html = await client.GetStringAsync("/en-US/Customer/Home/Index");
        Assert.DoesNotContain("Preview: wholesale pricing", html);
        var retail = 100m.ToString("c", new System.Globalization.CultureInfo("en-US"));
        Assert.Contains(retail, html);
    }

    [Fact]
    public async Task CompanyUser_AlwaysSeesWholesale()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        SeedProduct(factory, retail: 100m, wholesale: 70m);
        await TestAuthHelper.LoginAsync(client, factory.CompanyEmail, factory.TestPassword);
        var html = await client.GetStringAsync("/en-US/Customer/Home/Index");
        var wholesale = 70m.ToString("c", new System.Globalization.CultureInfo("en-US"));
        Assert.Contains(wholesale, html);
    }

    [Fact]
    public async Task Cart_AsAdminWholesalePreview_UsesWholesaleTotal()
    {
        using var factory = new CustomWebApplicationFactory();
        var adminEmail = await SeedAdminAsync(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        SeedProductAndCartForUser(factory, adminEmail, count: 2, retail: 100m, wholesale: 70m);
        await TestAuthHelper.LoginAsync(client, adminEmail, factory.TestPassword);

        var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Customer/Home/Index");
        await client.PostAsync("/en-US/Customer/Home/SetPreviewMode", new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["__RequestVerificationToken"] = token,
            ["mode"] = "wholesale",
            ["returnUrl"] = "/en-US/Customer/Cart/Index"
        }));

        var html = await client.GetStringAsync("/en-US/Customer/Cart/Index");
        var wholesaleTotal = 140m.ToString("c", new System.Globalization.CultureInfo("en-US"));
        Assert.Contains(wholesaleTotal, html);
    }

    private static void SeedProduct(WebApplicationFactory<Program> factory, decimal retail, decimal wholesale)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cat = new Category { Name = "CatTest", AvgShippingCost = 10m };
        var prod = new Product { Name = "ProdTest", Description = "desc", MaxExpectation = 10, Category = cat, ListPrice = retail, FinalRetailPrice = retail, FinalWholesalePrice = wholesale, IsAvailableInStore = true, IsDeleted = false, StockQuantity = 10 };
        db.Categories.Add(cat);
        db.Products.Add(prod);
        db.SaveChanges();
    }

    private static void SeedProductAndCartForUser(WebApplicationFactory<Program> factory, string email, int count, decimal retail, decimal wholesale)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = db.ApplicationUsers.Single(u => u.Email == email);
        var cat = new Category { Name = "CatCart", AvgShippingCost = 10m };
        var prod = new Product { Name = "ProdCart", Description = "desc", MaxExpectation = 10, Category = cat, ListPrice = retail, FinalRetailPrice = retail, FinalWholesalePrice = wholesale, IsAvailableInStore = true, IsDeleted = false, StockQuantity = 100 };
        db.Categories.Add(cat);
        db.Products.Add(prod);
        db.ShoppingCarts.Add(new ShoppingCart { ApplicationUserId = user.Id, Product = prod, Count = count });
        db.SaveChanges();
    }

    private static async Task<string> SeedAdminAsync(CustomWebApplicationFactory factory)
    {
        const string adminEmail = "admin.preview@vaultshop.local";
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await userManager.FindByEmailAsync(adminEmail) != null) return adminEmail;
        var user = new ApplicationUser { UserName = adminEmail, Email = adminEmail, Name = "Admin Preview", EmailConfirmed = true };
        await userManager.CreateAsync(user, factory.TestPassword);
        await userManager.AddToRoleAsync(user, SD.Role_Admin);
        return adminEmail;
    }

    private static async Task<string> SeedEmployeeAsync(CustomWebApplicationFactory factory)
    {
        const string employeeEmail = "employee.preview@vaultshop.local";
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await userManager.FindByEmailAsync(employeeEmail) != null) return employeeEmail;
        var user = new ApplicationUser { UserName = employeeEmail, Email = employeeEmail, Name = "Employee Preview", EmailConfirmed = true };
        await userManager.CreateAsync(user, factory.TestPassword);
        await userManager.AddToRoleAsync(user, SD.Role_Employee);
        return employeeEmail;
    }

    [Fact]
    public async Task SetPreviewMode_AsAnonymous_IsRejectedAndPriceRemainsRetail()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        SeedProduct(factory, retail: 100m, wholesale: 70m);

        var resp = await client.PostAsync("/en-US/Customer/Home/SetPreviewMode", new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["mode"] = "wholesale",
            ["returnUrl"] = "/en-US/Customer/Home/Index"
        }));

        Assert.NotEqual(HttpStatusCode.OK, resp.StatusCode);
        var html = await client.GetStringAsync("/en-US/Customer/Home/Index");
        var wholesale = 70m.ToString("c", new System.Globalization.CultureInfo("en-US"));
        var retail = 100m.ToString("c", new System.Globalization.CultureInfo("en-US"));
        Assert.DoesNotContain(wholesale, html);
        Assert.Contains(retail, html);
    }

    [Fact]
    public async Task SetPreviewMode_AsEmployee_SwitchesPriceAndShowsBanner()
    {
        using var factory = new CustomWebApplicationFactory();
        var employeeEmail = await SeedEmployeeAsync(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        SeedProduct(factory, retail: 100m, wholesale: 70m);
        await TestAuthHelper.LoginAsync(client, employeeEmail, factory.TestPassword);

        var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Customer/Home/Index");
        var resp = await client.PostAsync("/en-US/Customer/Home/SetPreviewMode", new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["__RequestVerificationToken"] = token,
            ["mode"] = "wholesale",
            ["returnUrl"] = "/en-US/Customer/Home/Index"
        }));
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);

        var html = await client.GetStringAsync("/en-US/Customer/Home/Index");
        var wholesale = 70m.ToString("c", new System.Globalization.CultureInfo("en-US"));
        Assert.Contains(wholesale, html);
        Assert.Contains("Preview: wholesale pricing", html);
        Assert.Contains("role=\"status\"", html);
    }

    private sealed class FakeSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();
        public bool IsAvailable => true;
        public string Id => Guid.NewGuid().ToString();
        public IEnumerable<string> Keys => _store.Keys;
        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}
