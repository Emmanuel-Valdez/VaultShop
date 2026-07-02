using VaultShop.Web.Services.Branding;

namespace VaultShop.Web.Tests;

public sealed class ThemeOptionsTests
{
	[Fact]
	public void CssColor_ExpandsShortHex()
	{
		var options = new ThemeOptions { Primary = "#AbC" };

		Assert.Equal("#aabbcc", options.PrimaryCss);
		Assert.Equal("170, 187, 204", options.PrimaryRgb);
	}

	[Fact]
	public void CssColor_FallsBackWhenConfiguredValueIsUnsafe()
	{
		var options = new ThemeOptions { Accent = "red;body{display:none}" };

		Assert.Equal(ThemeOptions.DefaultAccent, options.AccentCss);
		Assert.Equal("212, 160, 23", options.AccentRgb);
	}

	[Fact]
	public void CssColor_AllowsEightDigitHex()
	{
		var options = new ThemeOptions { Surface = "#11223344" };

		Assert.Equal("#11223344", options.SurfaceCss);
		Assert.Equal("17, 34, 51", options.SurfaceRgb);
	}
}
