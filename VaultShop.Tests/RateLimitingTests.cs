using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VaultShop.Web.Services.RateLimiting;

namespace VaultShop.Web.Tests;

public class RateLimitingTests
{
    private static WebApplicationFactory<Program> CreateFactory(Dictionary<string, string?> settings)
        => new CustomWebApplicationFactory().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings)));

    private static HttpClient CreateClient(Dictionary<string, string?> settings)
        => CreateFactory(settings).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public void GlobalLimit_DefaultsTo300RequestsPerMinute()
    {
        using var factory = new CustomWebApplicationFactory();

        var options = factory.Services.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        Assert.Equal(300, options.GlobalPermitLimit);
        Assert.Equal(60, options.GlobalWindowSeconds);
        Assert.Equal(0, options.GlobalQueueLimit);
    }

    [Fact]
    public async Task SiteManifest_IsServed()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/site.webmanifest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("VaultShop", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task StaticWellKnownAndErrorPaths_AreExemptFromTheGlobalBudget()
    {
        var client = CreateClient(new Dictionary<string, string?> { ["RateLimiting:GlobalPermitLimit"] = "1" });

        foreach (var path in new[] { "/images/missing.png", "/js/missing.js.map", "/.well-known/security.txt" })
        {
            var response = await client.GetAsync(path);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        // The budget is untouched by the missed static/well-known paths.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/en-US/Customer/Home/Index")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/en-US/Customer/Home/Index")).StatusCode);

        // The error page stays reachable after the client is limited.
        var error = await client.GetAsync("/Home/Error?statusCode=429");
        Assert.NotEqual(HttpStatusCode.TooManyRequests, error.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedUsers_SharingAnIp_HaveIndependentBudgets()
    {
        using var factory = CreateFactory(new Dictionary<string, string?> { ["RateLimiting:GlobalPermitLimit"] = "5" });
        using var customer = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var admin = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await TestAuthHelper.LoginAsync(customer, TestDataSeeder.CustomerEmail, TestDataSeeder.Password);
        await TestAuthHelper.LoginAsync(admin, TestDataSeeder.AdminEmail, TestDataSeeder.Password);

        for (var i = 0; i < 5; i++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await customer.GetAsync("/en-US/Customer/Home/Index")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await customer.GetAsync("/en-US/Customer/Home/Index")).StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await admin.GetAsync("/en-US/Customer/Home/Index")).StatusCode);
    }

    [Fact]
    public async Task AdminRequests_AreSubjectToTheGlobalLimit()
    {
        using var factory = CreateFactory(new Dictionary<string, string?> { ["RateLimiting:GlobalPermitLimit"] = "2" });
        using var admin = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await TestAuthHelper.LoginAsync(admin, TestDataSeeder.AdminEmail, TestDataSeeder.Password);

        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await admin.GetAsync("/en-US/Admin/Order")).StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await admin.GetAsync("/en-US/Admin/Order")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await admin.GetAsync("/en-US/Admin/Order")).StatusCode);
    }

    [Fact]
    public async Task Login_GetRequests_UseOnlyGlobalLimit()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Login GETs must not consume the 10/min credential POST budget.
        for (var i = 0; i < 20; i++)
        {
            var response = await client.GetAsync("/Identity/Account/Login");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task Login_PostRequests_AreLimitedAndPageStaysAvailable()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        for (var i = 0; i < 10; i++)
        {
            var response = await client.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Email"] = "nobody@example.test",
                ["Input.Password"] = "not-a-real-password",
            }));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var rejected = await client.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = "nobody@example.test",
            ["Input.Password"] = "not-a-real-password",
        }));
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);

        // A GET must still render the page while the POST budget is exhausted.
        var page = await client.GetAsync("/Identity/Account/Login");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
    }

    [Fact]
    public async Task Checkout_Burst_IsRejected()
    {
        var client = CreateClient(new Dictionary<string, string?> { ["RateLimiting:CheckoutPermitLimit"] = "1" });

        var first = await client.PostAsync("/en-US/Customer/Cart/Summary", new FormUrlEncodedContent(new Dictionary<string, string>()));
        var second = await client.PostAsync("/en-US/Customer/Cart/Summary", new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.NotEqual(HttpStatusCode.TooManyRequests, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Fact]
    public async Task CartMutation_Burst_IsRejected()
    {
        var client = CreateClient(new Dictionary<string, string?> { ["RateLimiting:CartPermitLimit"] = "1" });

        var first = await client.PostAsync("/en-US/Customer/Cart/Plus?cartId=1", new FormUrlEncodedContent(new Dictionary<string, string>()));
        var second = await client.PostAsync("/en-US/Customer/Cart/Plus?cartId=1", new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.NotEqual(HttpStatusCode.TooManyRequests, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Fact]
    public async Task Search_Burst_IsRejected()
    {
        var client = CreateClient(new Dictionary<string, string?> { ["RateLimiting:SearchPermitLimit"] = "1" });

        var first = await client.GetAsync("/en-US/Customer/Home/Search?searchString=test");
        var second = await client.GetAsync("/en-US/Customer/Home/Search?searchString=test");

        Assert.NotEqual(HttpStatusCode.TooManyRequests, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Fact]
    public async Task PasswordRecovery_Burst_IsRejected()
    {
        var client = CreateClient(new Dictionary<string, string?> { ["RateLimiting:RecoveryPermitLimit"] = "1" });

        var first = await client.PostAsync("/Identity/Account/ForgotPassword", new FormUrlEncodedContent(new Dictionary<string, string>()));
        var second = await client.PostAsync("/Identity/Account/ForgotPassword", new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.NotEqual(HttpStatusCode.TooManyRequests, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Theory]
    [InlineData("en-US", "Too many requests", "Try again")]
    [InlineData("es-AR", "Demasiadas solicitudes", "Intentar de nuevo")]
    public async Task RejectedRequest_ReturnsLocalizedPageWithRetryAfter(string culture, string title, string retryLabel)
    {
        var client = CreateClient(new Dictionary<string, string?> { ["RateLimiting:GlobalPermitLimit"] = "1" });
        var url = $"/{culture}/Customer/Home/Index";

        var first = await client.GetAsync(url);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, first.StatusCode);

        var rejected = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.TryGetValues("Retry-After", out var values));
        Assert.True(int.TryParse(values.First(), out var seconds) && seconds > 0);

        var body = await rejected.Content.ReadAsStringAsync();
        Assert.Contains(title, body);
        Assert.Contains(retryLabel, body);
        Assert.Contains("429", body);
    }

    [Fact]
    public async Task PaymentWebhooks_AreExemptFromTheGlobalLimit()
    {
        var client = CreateClient(new Dictionary<string, string?> { ["RateLimiting:GlobalPermitLimit"] = "1" });

        for (var i = 0; i < 2; i++)
        {
            var stripe = await client.PostAsync("/api/stripe/webhook", new StringContent("{}"));
            var mercadoPago = await client.PostAsync("/api/mercadopago/webhook", new StringContent("{}"));

            Assert.NotEqual(HttpStatusCode.TooManyRequests, stripe.StatusCode);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, mercadoPago.StatusCode);
        }
    }
}
