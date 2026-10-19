using VaultShop.Web.Areas.Admin.Controllers;

namespace VaultShop.Web.Tests;

// envio-gratis 2.3 — the price-calculator help documents the free-shipping flag's effect in both cultures.
public class PricingSettingsHelpLocalizationTests
{
	private static readonly System.Resources.ResourceManager IndexResources = new(
		"VaultShop.Web.Resources.Areas.Admin.Views.ProductPrice.Index",
		typeof(ProductPriceController).Assembly);

	[Theory]
	[InlineData("en-US", "free shipping enabled")]
	[InlineData("es-AR", "envío gratis activado")]
	public void PricingSettingsDescription_DocumentsFreeShippingFlag(string culture, string expectedFragment)
	{
		var cultureInfo = System.Globalization.CultureInfo.GetCultureInfo(culture);

		var help = IndexResources.GetString("PricingSettingsDescription", cultureInfo);

		Assert.NotNull(help);
		Assert.Contains(expectedFragment, help);
	}
}
