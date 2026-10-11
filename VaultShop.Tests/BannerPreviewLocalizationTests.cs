using VaultShop.Web.Areas.Admin.Controllers;

namespace VaultShop.Web.Tests;

// banner-preview-safe-zone 3.5: preview/editor resx keys resolve in both cultures.
public class BannerPreviewLocalizationTests
{
	private static readonly System.Resources.ResourceManager UpsertResources = new(
		"VaultShop.Web.Resources.Areas.Admin.Views.Banner.Upsert",
		typeof(BannerController).Assembly);
	private static readonly System.Resources.ResourceManager ControllerResources = new(
		"VaultShop.Web.Resources.Areas.Admin.Controllers.BannerController",
		typeof(BannerController).Assembly);

	[Theory]
	[InlineData("en-US", "Live preview", "Desktop", "Mobile", "No safe zone")]
	[InlineData("es-AR", "Vista previa", "Escritorio", "Móvil", "Sin zona segura")]
	public void Upsert_PreviewKeys_ResolveInBothCultures(string culture, string preview, string desktop, string mobile, string reset)
	{
		var cultureInfo = System.Globalization.CultureInfo.GetCultureInfo(culture);

		Assert.Equal(preview, UpsertResources.GetString("Preview", cultureInfo));
		Assert.Equal(desktop, UpsertResources.GetString("PreviewDesktop", cultureInfo));
		Assert.Equal(mobile, UpsertResources.GetString("PreviewMobile", cultureInfo));
		Assert.Equal(reset, UpsertResources.GetString("SafeZoneReset", cultureInfo));
		Assert.NotNull(UpsertResources.GetString("SafeZoneHint", cultureInfo));
		Assert.NotNull(UpsertResources.GetString("SafeZoneEditor", cultureInfo));
		Assert.NotNull(UpsertResources.GetString("NoPreviewImage", cultureInfo));
		// banner-preview-safe-zone 5.5: mobile image is ImageLed-only.
		Assert.NotNull(UpsertResources.GetString("MobileImageLayoutHint", cultureInfo));
	}

	[Theory]
	[InlineData("en-US")]
	[InlineData("es-AR")]
	public void Upsert_SafeZoneInvalid_ResolveInBothCultures(string culture)
	{
		var cultureInfo = System.Globalization.CultureInfo.GetCultureInfo(culture);

		Assert.NotNull(ControllerResources.GetString("SafeZoneInvalid", cultureInfo));
	}
}
