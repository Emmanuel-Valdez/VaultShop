using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using VaultShop.DataAccess.Data;
using VaultShop.Models;

namespace VaultShop.Web.Tests;

public class BannerAdminHttpTests
{
	[Fact]
	public async Task Index_RendersTable()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);

		var response = await client.GetAsync("/en-US/Admin/Banner/Index");
		var html = await response.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Contains("tblData", html);
		Assert.Contains("banner.js", html);
	}

	[Fact]
	public async Task Upsert_New_RendersForm()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);

		var response = await client.GetAsync("/en-US/Admin/Banner/Upsert");
		var html = await response.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Contains("name=\"coverFile\"", html);
		Assert.Contains("name=\"mobileFile\"", html);
		Assert.Contains("Banner_Layout", html);
		Assert.Contains("Banner_TextColor", html);
		Assert.Contains("Banner_TargetKind", html);
		Assert.Contains("Banner_TargetId", html);
		Assert.Contains("Banner_SortOrder", html);
		Assert.Contains("Banner_IsActive", html);
		// Live preview panel with the safe-zone editor (works for new banners too).
		Assert.Contains("data-banner-preview", html);
		Assert.Contains("bannerPreview.js", html);
		Assert.Contains("Banner_SafeZoneLeft", html);
		Assert.Contains("Banner_SafeZoneTop", html);
		Assert.Contains("Banner_SafeZoneWidth", html);
		Assert.Contains("Banner_SafeZoneHeight", html);
	}

	[Fact]
	public async Task Upsert_Existing_RendersImagesPanel()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		var bannerId = SeedBanner(factory);
		await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);

		var response = await client.GetAsync($"/en-US/Admin/Banner/Upsert?id={bannerId}");
		var html = await response.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Contains("name=\"imageId\"", html);
		Assert.Contains("DeleteImage", html);
	}

	[Fact]
	public async Task Upsert_Edit_WithFiles_ReplacesImagesWithoutTrackingConflict()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		var bannerId = SeedBanner(factory);
		await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);

		var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, $"/en-US/Admin/Banner/Upsert?id={bannerId}");
		using var form = new MultipartFormDataContent();
		form.Add(new StringContent(bannerId.ToString()), "Banner.Id");
		form.Add(new StringContent("Invierno editado"), "Banner.Title");
		form.Add(new StringContent("Crossed"), "Banner.Layout");
		form.Add(new StringContent("BrandDark"), "Banner.TextColor");
		form.Add(new StringContent("Store"), "Banner.TargetKind");
		form.Add(new StringContent("0"), "Banner.SortOrder");
		form.Add(new StringContent("true"), "Banner.IsActive");
		form.Add(new StringContent(token), "__RequestVerificationToken");
		var cover = new ByteArrayContent(CreateCoverPngBytes());
		cover.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
		form.Add(cover, "coverFile", "cover.png");
		var mobile = new ByteArrayContent(CreateMobilePngBytes());
		mobile.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
		form.Add(mobile, "mobileFile", "mobile.png");

		// Regression: the edit path attaches the banner graph, so the replace flow must query
		// tracked rows or Remove throws "another instance with the same key is already being tracked".
		var response = await client.PostAsync("/en-US/Admin/Banner/Upsert", form);

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		using (var scope = factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var coverKinds = new[] { BannerImageKind.Master, BannerImageKind.Medium, BannerImageKind.Small };
			Assert.Equal(3, db.BannerImages.Count(i => i.BannerId == bannerId && coverKinds.Contains(i.Kind)));
			Assert.Single(db.BannerImages.Where(i => i.BannerId == bannerId && i.Kind == BannerImageKind.Mobile));
			Assert.Equal("Invierno editado", db.Banners.Single(b => b.Id == bannerId).Title);
		}
	}

	[Fact]
	public async Task Upsert_LocalizedEs_RendersLocalizedDropdowns()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);
		using (var scope = factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			db.Categories.Add(new Category { Name = "Mochilas" });
			db.SaveChanges();
		}

		var response = await client.GetAsync("/es-AR/Admin/Banner/Upsert");
		var html = await response.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Contains("Cruzado", html);
		Assert.Contains("Con imagen protagonista", html);
		Assert.Contains("Color de marca oscuro", html);
		Assert.Contains("Tienda", html);
		// Razor encodes non-ASCII option text: "Categorías".
		Assert.Contains("Categor&#xED;as", html);
	}

	[Fact]
	public async Task GetAll_LocalizedEs_ReturnsLocalizedLabels()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		var bannerId = SeedBanner(factory);
		await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);
		using (var scope = factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var banner = db.Banners.Single(b => b.Id == bannerId);
			banner.Layout = BannerLayout.Crossed;
			banner.TargetKind = BannerTargetKind.Category;
			banner.TargetId = 1;
			db.SaveChanges();
		}

		var response = await client.GetAsync("/es-AR/Admin/Banner/GetAll");
		var json = await response.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Contains("Cruzado", json);
		// System.Text.Json escapes non-ASCII: "Categoría".
		Assert.Contains("Categor\\u00EDa", json);
	}

	[Fact]
	public async Task Upsert_ValidRect_RoundTripsThroughPostAndReload()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);

		var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Admin/Banner/Upsert");
		using var form = BaseBannerForm("0", token);
		form.Add(new StringContent("0.100"), "Banner.SafeZoneLeft");
		form.Add(new StringContent("0.200"), "Banner.SafeZoneTop");
		form.Add(new StringContent("0.500"), "Banner.SafeZoneWidth");
		form.Add(new StringContent("0.400"), "Banner.SafeZoneHeight");
		var cover = new ByteArrayContent(CreateCoverPngBytes());
		cover.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
		form.Add(cover, "coverFile", "cover.png");

		var response = await client.PostAsync("/en-US/Admin/Banner/Upsert", form);

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		int bannerId;
		using (var scope = factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var banner = db.Banners.Single(b => b.Title == "Zona");
			Assert.Equal(0.100m, banner.SafeZoneLeft);
			Assert.Equal(0.200m, banner.SafeZoneTop);
			Assert.Equal(0.500m, banner.SafeZoneWidth);
			Assert.Equal(0.400m, banner.SafeZoneHeight);
			bannerId = banner.Id;
		}

		var reload = await client.GetAsync($"/en-US/Admin/Banner/Upsert?id={bannerId}");
		var html = await reload.Content.ReadAsStringAsync();
		Assert.Equal(HttpStatusCode.OK, reload.StatusCode);
		Assert.Contains("--banner-safe-top:20%", html);
		Assert.Contains("--banner-safe-left:10%", html);
	}

	[Fact]
	public async Task Upsert_PartialRect_FailsValidationAndDoesNotPersist()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);

		var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Admin/Banner/Upsert");
		using var form = BaseBannerForm("0", token);
		form.Add(new StringContent("0.100"), "Banner.SafeZoneLeft");
		form.Add(new StringContent("0.200"), "Banner.SafeZoneTop");
		var cover = new ByteArrayContent(CreateCoverPngBytes());
		cover.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
		form.Add(cover, "coverFile", "cover.png");

		var response = await client.PostAsync("/en-US/Admin/Banner/Upsert", form);
		var html = await response.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Contains("safe zone", html, StringComparison.OrdinalIgnoreCase);
		using (var scope = factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			Assert.Empty(db.Banners.Where(b => b.Title == "Zona"));
		}
	}

	[Fact]
	public async Task Upsert_OutOfBoundsRect_FailsValidationAndDoesNotPersist()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);

		var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Admin/Banner/Upsert");
		using var form = BaseBannerForm("0", token);
		form.Add(new StringContent("0.800"), "Banner.SafeZoneLeft");
		form.Add(new StringContent("0.100"), "Banner.SafeZoneTop");
		form.Add(new StringContent("0.500"), "Banner.SafeZoneWidth");
		form.Add(new StringContent("0.100"), "Banner.SafeZoneHeight");
		var cover = new ByteArrayContent(CreateCoverPngBytes());
		cover.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
		form.Add(cover, "coverFile", "cover.png");

		var response = await client.PostAsync("/en-US/Admin/Banner/Upsert", form);
		var html = await response.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Contains("safe zone", html, StringComparison.OrdinalIgnoreCase);
		using (var scope = factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			Assert.Empty(db.Banners.Where(b => b.Title == "Zona"));
		}
	}

	[Fact]
	public async Task Upsert_EmptyRect_ClearsPriorValue()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		var bannerId = SeedBanner(factory);
		using (var scope = factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var banner = db.Banners.Single(b => b.Id == bannerId);
			banner.SafeZoneLeft = 0.1m;
			banner.SafeZoneTop = 0.2m;
			banner.SafeZoneWidth = 0.5m;
			banner.SafeZoneHeight = 0.4m;
			db.SaveChanges();
		}
		await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);

		var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, $"/en-US/Admin/Banner/Upsert?id={bannerId}");
		using var form = BaseBannerForm(bannerId.ToString(), token);
		form.Add(new StringContent(""), "Banner.SafeZoneLeft");
		form.Add(new StringContent(""), "Banner.SafeZoneTop");
		form.Add(new StringContent(""), "Banner.SafeZoneWidth");
		form.Add(new StringContent(""), "Banner.SafeZoneHeight");

		var response = await client.PostAsync("/en-US/Admin/Banner/Upsert", form);

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		using (var scope = factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var banner = db.Banners.Single(b => b.Id == bannerId);
			Assert.Null(banner.SafeZoneLeft);
			Assert.Null(banner.SafeZoneTop);
			Assert.Null(banner.SafeZoneWidth);
			Assert.Null(banner.SafeZoneHeight);
		}
	}

	private static MultipartFormDataContent BaseBannerForm(string id, string token, string layout = "Crossed")
	{
		var form = new MultipartFormDataContent();
		form.Add(new StringContent(id), "Banner.Id");
		form.Add(new StringContent("Zona"), "Banner.Title");
		form.Add(new StringContent(layout), "Banner.Layout");
		form.Add(new StringContent("White"), "Banner.TextColor");
		form.Add(new StringContent("Store"), "Banner.TargetKind");
		form.Add(new StringContent("0"), "Banner.SortOrder");
		form.Add(new StringContent("true"), "Banner.IsActive");
		form.Add(new StringContent(token), "__RequestVerificationToken");
		return form;
	}

	private static int SeedBanner(CustomWebApplicationFactory factory)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var banner = new Banner { Title = "Invierno", SortOrder = 1, IsActive = true };
		db.Banners.Add(banner);
		db.SaveChanges();
		db.BannerImages.Add(new BannerImage
		{
			BannerId = banner.Id,
			Kind = BannerImageKind.Master,
			ImageUrl = "https://example.test/cover.jpg",
			ObjectKey = "banners/banner-1/cover.jpg",
			FileName = "cover.jpg",
			ContentType = "image/jpeg",
			SizeBytes = 10,
			StorageProvider = "Local",
		});
		db.BannerImages.Add(new BannerImage
		{
			BannerId = banner.Id,
			Kind = BannerImageKind.Mobile,
			ImageUrl = "https://example.test/mobile.jpg",
			ObjectKey = "banners/banner-1/mobile.jpg",
			FileName = "mobile.jpg",
			ContentType = "image/jpeg",
			SizeBytes = 10,
			StorageProvider = "Local",
		});
		db.SaveChanges();
		return banner.Id;
	}

	// banner-preview-safe-zone 5.6: switching a Crossed banner with a mobile asset to ImageLed
	// persists and the storefront home emits the art-directed <picture> + mobile <source>.
	[Fact]
	public async Task Upsert_CrossedToImageLed_PersistsLayoutAndHomeEmitsMobileSource()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		var bannerId = SeedBanner(factory);
		await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);

		var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, $"/en-US/Admin/Banner/Upsert?id={bannerId}");
		using var form = BaseBannerForm(bannerId.ToString(), token, layout: "ImageLed");

		var response = await client.PostAsync("/en-US/Admin/Banner/Upsert", form);

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		using (var scope = factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			Assert.Equal(BannerLayout.ImageLed, db.Banners.Single(b => b.Id == bannerId).Layout);
		}

		var html = await client.GetStringAsync("/en-US/Customer/Home/Index");
		Assert.Contains("<picture>", html);
		Assert.Contains("(max-width: 991.98px)", html);
	}

	private static byte[] CreateCoverPngBytes()
	{
		using var bitmap = new SKBitmap(2100, 900);
		bitmap.Erase(SKColors.Red);
		using var image = SKImage.FromBitmap(bitmap);
		using var data = image.Encode(SKEncodedImageFormat.Png, 100);
		return data.ToArray();
	}

	private static byte[] CreateMobilePngBytes()
	{
		using var bitmap = new SKBitmap(1500, 2000);
		bitmap.Erase(SKColors.Blue);
		using var image = SKImage.FromBitmap(bitmap);
		using var data = image.Encode(SKEncodedImageFormat.Png, 100);
		return data.ToArray();
	}
}
