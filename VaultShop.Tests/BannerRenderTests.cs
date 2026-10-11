using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VaultShop.DataAccess.Data;
using VaultShop.Models;

namespace VaultShop.Web.Tests;

// home-banners 6.1: text-color -> class mapping and mobile-asset branches.
public class BannerRenderTests
{
	[Theory]
	[InlineData(BannerTextColor.BrandLight, "banner--color-brandlight")]
	[InlineData(BannerTextColor.BrandDark, "banner--color-branddark")]
	[InlineData(BannerTextColor.White, "banner--color-white")]
	[InlineData(BannerTextColor.Black, "banner--color-black")]
	public async Task Index_TextColor_MapsToExpectedClass(BannerTextColor color, string expectedClass)
	{
		using var factory = new CustomWebApplicationFactory();
		Seed(factory, new Banner { Title = "T", SortOrder = 1, IsActive = true, TextColor = color });
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

		var html = await client.GetStringAsync("/en-US/Customer/Home/Index");

		Assert.Contains(expectedClass, html);
	}

	[Fact]
	public async Task Index_SlideAriaLabel_IsFormatted()
	{
		using var factory = new CustomWebApplicationFactory();
		Seed(factory, new Banner { Title = "T", SortOrder = 1, IsActive = true });
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

		var html = await client.GetStringAsync("/en-US/Customer/Home/Index");
		var carousel = html.Substring(html.IndexOf("data-home-banner", StringComparison.Ordinal));

		Assert.Contains("aria-label=\"Slide 1 of 1: T\"", carousel);
		Assert.DoesNotContain("{0}", carousel);
	}

	[Fact]
	public async Task Index_ImageLedWithMobile_RendersPictureSource()
	{
		using var factory = new CustomWebApplicationFactory();
		Seed(factory,
			new Banner { Title = "T", SortOrder = 1, IsActive = true, Layout = BannerLayout.ImageLed },
			withMobile: true);
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

		var html = await client.GetStringAsync("/en-US/Customer/Home/Index");

		Assert.Contains("<picture>", html);
		Assert.Contains("(max-width: 991.98px)", html);
	}

	[Fact]
	public async Task Index_ImageLedWithoutMobile_RendersNoPicture()
	{
		using var factory = new CustomWebApplicationFactory();
		Seed(factory,
			new Banner { Title = "T", SortOrder = 1, IsActive = true, Layout = BannerLayout.ImageLed });
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

		var html = await client.GetStringAsync("/en-US/Customer/Home/Index");
		var carousel = html.Substring(html.IndexOf("data-home-banner", StringComparison.Ordinal));

		Assert.DoesNotContain("<picture>", carousel);
		Assert.Contains("banner__image", carousel);
	}

	[Fact]
	public async Task Index_CrossedWithMobile_RendersNoPicture()
	{
		using var factory = new CustomWebApplicationFactory();
		Seed(factory,
			new Banner { Title = "T", SortOrder = 1, IsActive = true, Layout = BannerLayout.Crossed },
			withMobile: true);
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

		var html = await client.GetStringAsync("/en-US/Customer/Home/Index");
		var carousel = html.Substring(html.IndexOf("data-home-banner", StringComparison.Ordinal));

		// Art-direction is ImageLed-only; Crossed falls back to the CSS crop.
		Assert.DoesNotContain("<picture>", carousel);
	}

	[Fact]
	public async Task Index_PartialCoverSet_FallsBackToMedium()
	{
		using var factory = new CustomWebApplicationFactory();
		var banner = new Banner { Title = "T", SortOrder = 1, IsActive = true };
		Seed(factory, banner, withMobile: false, withMaster: false);
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

		var html = await client.GetStringAsync("/en-US/Customer/Home/Index");
		var carousel = html.Substring(html.IndexOf("data-home-banner", StringComparison.Ordinal));

		// No Master row (source too small for 1905x714): the slide must still show a real image.
		Assert.Contains("src=\"https://example.test/medium.jpg\"", carousel);
	}

	[Fact]
	public async Task Index_SafeZone_EmitsInsetVars()
	{
		using var factory = new CustomWebApplicationFactory();
		Seed(factory, new Banner
		{
			Title = "T",
			SortOrder = 1,
			IsActive = true,
			SafeZoneLeft = 0.1m,
			SafeZoneTop = 0.2m,
			SafeZoneWidth = 0.5m,
			SafeZoneHeight = 0.4m,
		});
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

		var html = await client.GetStringAsync("/en-US/Customer/Home/Index");
		var carousel = html.Substring(html.IndexOf("data-home-banner", StringComparison.Ordinal));

		Assert.Contains("--banner-safe-top:20%", carousel);
		Assert.Contains("--banner-safe-right:40%", carousel);
		Assert.Contains("--banner-safe-bottom:40%", carousel);
		Assert.Contains("--banner-safe-left:10%", carousel);
	}

	[Fact]
	public async Task Index_NoSafeZone_EmitsNoInsetVars()
	{
		using var factory = new CustomWebApplicationFactory();
		Seed(factory, new Banner { Title = "T", SortOrder = 1, IsActive = true });
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

		var html = await client.GetStringAsync("/en-US/Customer/Home/Index");
		var carousel = html.Substring(html.IndexOf("data-home-banner", StringComparison.Ordinal));

		Assert.DoesNotContain("--banner-safe-", carousel);
	}

	private static void Seed(CustomWebApplicationFactory factory, Banner banner, bool withMobile = false, bool withMaster = true)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		db.Banners.Add(banner);
		db.SaveChanges();
		if (withMaster)
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
		db.BannerImages.Add(new BannerImage
		{
			BannerId = banner.Id,
			Kind = BannerImageKind.Medium,
			ImageUrl = "https://example.test/medium.jpg",
			ObjectKey = $"banners/banner-{banner.Id}/medium.jpg",
			FileName = "medium.jpg",
			ContentType = "image/jpeg",
			SizeBytes = 10,
			StorageProvider = "Local",
		});
		db.BannerImages.Add(new BannerImage
		{
			BannerId = banner.Id,
			Kind = BannerImageKind.Small,
			ImageUrl = "https://example.test/small.jpg",
			ObjectKey = $"banners/banner-{banner.Id}/small.jpg",
			FileName = "small.jpg",
			ContentType = "image/jpeg",
			SizeBytes = 10,
			StorageProvider = "Local",
		});
		if (withMobile)
		{
			db.BannerImages.Add(new BannerImage
			{
				BannerId = banner.Id,
				Kind = BannerImageKind.Mobile,
				ImageUrl = "https://example.test/mobile.jpg",
				ObjectKey = $"banners/banner-{banner.Id}/mobile.jpg",
				FileName = "mobile.jpg",
				ContentType = "image/jpeg",
				SizeBytes = 10,
				StorageProvider = "Local",
			});
		}
		db.SaveChanges();
	}
}
