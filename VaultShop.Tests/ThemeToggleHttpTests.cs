using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace VaultShop.Web.Tests;

public class ThemeToggleHttpTests
{
	private const string ThemeToggle = "data-theme-toggle";

	[Fact]
	public async Task StorefrontHome_RendersToggleOnlyInHeader_NoFloatingControl()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient();

		var body = await client.GetStringAsync("/en-US/Customer/Home/Index");

		// Old floating dropdown control is gone.
		Assert.DoesNotContain("bd-mode-toggle", body);
		Assert.DoesNotContain("data-bs-theme-value", body);
		Assert.DoesNotContain(">Auto<", body);
		Assert.DoesNotContain("position-fixed bottom-0 end-0", body);

		// Exactly the two shared-layout placements: offcanvas + desktop navbar.
		Assert.Equal(2, Regex.Matches(body, ThemeToggle).Count);

		var offcanvas = ExtractSideNavbar(body);
		Assert.Single(Regex.Matches(offcanvas, ThemeToggle));

		var header = Regex.Match(body, "<header[^>]*>.*?</header>", RegexOptions.Singleline);
		Assert.True(header.Success, "Header block not found.");
		Assert.Equal(2, Regex.Matches(header.Value, ThemeToggle).Count);

		Assert.DoesNotContain(ThemeToggle, ExtractMobileNav(body));
	}

	[Fact]
	public async Task IdentityLogin_RendersToggleTwice_OneInSideNav()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient();

		var body = await client.GetStringAsync("/Identity/Account/Login");

		Assert.Equal(2, Regex.Matches(body, ThemeToggle).Count);
		var offcanvas = ExtractSideNavbar(body);
		Assert.Single(Regex.Matches(offcanvas, ThemeToggle));
	}

	[Fact]
	public async Task AdminPage_RendersToggleTwice_OneInSideNav()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);

		var response = await client.GetAsync("/en-US/Admin/Order/Index");
		var body = await response.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal(2, Regex.Matches(body, ThemeToggle).Count);
		var offcanvas = ExtractSideNavbar(body);
		Assert.Single(Regex.Matches(offcanvas, ThemeToggle));
	}

	private static string ExtractSideNavbar(string body)
	{
		var match = Regex.Match(body, "<section[^>]*id=\"sideNavbarTop\".*?</section>", RegexOptions.Singleline);
		Assert.True(match.Success, "sideNavbarTop section not found.");
		return match.Value;
	}

	// Razor CSS isolation injects a scope attribute between <nav and class, so a literal
	// attribute regex is brittle; slice from the class token to its closing </nav> instead.
	private static string ExtractMobileNav(string body)
	{
		var start = body.IndexOf("storefront-mobile-nav", StringComparison.Ordinal);
		Assert.True(start >= 0, "storefront-mobile-nav block not found.");
		var end = body.IndexOf("</nav>", start, StringComparison.Ordinal);
		Assert.True(end > start, "storefront-mobile-nav closing tag not found.");
		return body.Substring(start, end - start);
	}
}