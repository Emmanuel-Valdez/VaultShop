using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VaultShop.DataAccess.Data;
using VaultShop.Models;

namespace VaultShop.Web.Tests;

public class HomeBannerHttpTests
{
	[Fact]
	public async Task Index_RendersOnlyActiveBanners_InSortOrder()
	{
		using var factory = new CustomWebApplicationFactory();
		SeedBanners(factory);
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

		var html = await client.GetStringAsync("/en-US/Customer/Home/Index");

		Assert.Contains("data-home-banner", html);
		Assert.Contains("Segundo", html);
		Assert.Contains("Primero", html);
		Assert.DoesNotContain("Inactivo", html);
		// SortOrder ascending: Segundo (1) before Primero (2).
		Assert.True(html.IndexOf("Segundo", StringComparison.Ordinal) < html.IndexOf("Primero", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Index_WithTwoBanners_RendersControls_AndSingleSlideLinkPerSlide()
	{
		using var factory = new CustomWebApplicationFactory();
		SeedBanners(factory);
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

		var html = await client.GetStringAsync("/en-US/Customer/Home/Index");

		Assert.Contains("carousel-indicators", html);
		Assert.Contains("carousel-control-prev", html);
		Assert.Contains("carousel-control-next", html);
		Assert.Contains("data-banner-pause", html);
		// Store target resolves to the store home page.
		Assert.Contains("/en-US/Customer/Home/Index", html);
	}

	[Fact]
	public async Task Index_WithNoBanners_RendersNoCarousel()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

		var html = await client.GetStringAsync("/en-US/Customer/Home/Index");

		Assert.DoesNotContain("data-home-banner", html);
		Assert.DoesNotContain("homeBannerCarousel", html);
		// Page degrades to the catalog layout (empty catalog in a fresh database).
		Assert.Contains("empty-state", html);
	}

	[Fact]
	public async Task Index_Slide_EmitsNoHeadings_AndNonInteractiveCta()
	{
		using var factory = new CustomWebApplicationFactory();
		SeedBanners(factory);
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

		var html = await client.GetStringAsync("/en-US/Customer/Home/Index");
		var carousel = html.Substring(html.IndexOf("data-home-banner", StringComparison.Ordinal));
		carousel = carousel.Substring(0, carousel.IndexOf("</section>", StringComparison.Ordinal));

		Assert.DoesNotContain("<h1", carousel);
		Assert.DoesNotContain("<h2", carousel);
		Assert.DoesNotContain("<h3", carousel);
		Assert.Contains("banner__cta", carousel);
		Assert.DoesNotContain("<a", carousel.Substring(carousel.IndexOf("banner__cta", StringComparison.Ordinal), 200));
		Assert.DoesNotContain("<button", carousel.Substring(carousel.IndexOf("banner__cta", StringComparison.Ordinal), 200));
		Assert.Contains("role=\"group\"", carousel);
		Assert.Contains("aria-roledescription=\"slide\"", carousel);
		// Empty sections omitted: seed has no hook/body.
		Assert.DoesNotContain("banner__hook", carousel);
		Assert.DoesNotContain("banner__body", carousel);
	}

	[Fact]
	public async Task Index_SingleBanner_RendersNoControls()
	{
		using var factory = new CustomWebApplicationFactory();
		using (var scope = factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			db.Banners.Add(new Banner { Title = "Unico", SortOrder = 1, IsActive = true });
			db.SaveChanges();
		}
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

		var html = await client.GetStringAsync("/en-US/Customer/Home/Index");

		Assert.Contains("data-home-banner", html);
		Assert.DoesNotContain("carousel-indicators", html);
		Assert.DoesNotContain("carousel-control-prev", html);
		Assert.DoesNotContain("data-banner-pause", html);
	}

	private static void SeedBanners(CustomWebApplicationFactory factory)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		db.Banners.AddRange(
			new Banner { Title = "Primero", CallToAction = "Ver", SortOrder = 2, IsActive = true, TargetKind = BannerTargetKind.Store },
			new Banner { Title = "Segundo", CallToAction = "Ver", SortOrder = 1, IsActive = true, TargetKind = BannerTargetKind.Store },
			new Banner { Title = "Inactivo", SortOrder = 0, IsActive = false });
		db.SaveChanges();
		foreach (var banner in db.Banners.Where(b => b.IsActive))
		{
			db.BannerImages.Add(new BannerImage
			{
				BannerId = banner.Id,
				Kind = BannerImageKind.Master,
				ImageUrl = "https://example.test/cover.jpg",
				ObjectKey = $"banners/banner-{banner.Id}/cover.jpg",
				FileName = "cover.jpg",
				ContentType = "image/jpeg",
				SizeBytes = 10,
				StorageProvider = "Local",
			});
		}
		db.SaveChanges();
	}
}
