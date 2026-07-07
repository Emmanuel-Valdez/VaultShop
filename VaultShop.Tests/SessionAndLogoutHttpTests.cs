using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VaultShop.Models;

namespace VaultShop.Web.Tests;

public class SessionAndLogoutHttpTests
{
    [Fact]
    public async Task StaleSession_AfterUserDeleted_ChallengesWithSessionExpiredFlag()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

        // Delete the user directly — simulating account deletion after cookie issuance
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(factory.CustomerEmail);
            Assert.NotNull(user);
            var result = await userManager.DeleteAsync(user);
            Assert.True(result.Succeeded);
        }

        // The stale cookie should trigger OnValidatePrincipal ? RejectPrincipal + sessionExpired flag
        var response = await client.GetAsync("/en-US/Customer/Cart/Index");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        // Stale-session redirect is a relative URI (set by OnRedirectToLogin path override)
        Assert.StartsWith("/Identity/Account/Login?sessionExpired=true&returnUrl=",
            response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Logout_ClearsCookie_SubsequentRequestChallengesLogin()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

        // Step 1: verify authenticated access works
        var cartResponse = await client.GetAsync("/en-US/Customer/Cart/Index");
        Assert.Equal(HttpStatusCode.OK, cartResponse.StatusCode);

        // Extract antiforgery token from the authenticated page (shared layout has logout form with token)
        var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Customer/Cart/Index");

        // POST logout
        var logoutResponse = await client.PostAsync("/Identity/Account/Logout", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Redirect, logoutResponse.StatusCode);

        // Step 2: cookie is gone — [Authorize] challenge should redirect to Login (default, not sessionExpired)
        var afterLogoutResponse = await client.GetAsync("/en-US/Customer/Cart/Index");

        Assert.Equal(HttpStatusCode.Redirect, afterLogoutResponse.StatusCode);
        var afterLocation = afterLogoutResponse.Headers.Location!;
        Assert.Equal("/Identity/Account/Login", afterLocation.AbsolutePath);
        Assert.Contains("?ReturnUrl=", afterLocation.Query);
        Assert.DoesNotContain("sessionExpired", afterLocation.Query);
    }
}