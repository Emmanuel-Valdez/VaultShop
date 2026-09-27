using System.Reflection;
using System.Text.RegularExpressions;
using VaultShop.Web.Services.KeywordImages;

namespace VaultShop.Web.Tests;

// ponytail: guard linking crop constants to site.css --hero-h — a hero height change without
// a crop update fails here instead of silently reintroducing aggressive crops.
public class CoverCropGeometryTests
{
	[Fact]
	public void BandCrops_MatchHeroBox_WithinTolerances()
	{
		var large = Crop("CoverLargeWidth", "CoverLargeHeight"); // 1600x700, band 992+
		var medium = Crop("CoverMediumWidth", "CoverMediumHeight"); // 1200x500, band 480-991
		var small = Crop("CoverSmallWidth", "CoverSmallHeight"); // 780x520, band <480

		// Exact at the widths each crop is tuned for (design D1).
		AssertRatioEqual(BoxRatio(390), small, 0.001);
		AssertRatioEqual(BoxRatio(768), medium, 0.001);
		AssertRatioEqual(BoxRatio(1280), large, 0.001);

		// Band edges stay within tolerance (spec: <=25% sides, <=25% vertical mid, <=35% vertical wide).
		Assert.True(WidthDiscard(BoxRatio(480), medium) <= 0.25, "480px must discard at most 25% of width.");
		Assert.True(HeightDiscard(BoxRatio(991), medium) <= 0.25, "991px must keep at least 75% of height.");
		Assert.True(HeightDiscard(BoxRatio(1920), large) <= 0.35, "1920px must discard at most 35% of height.");
		Assert.True(WidthDiscard(BoxRatio(320), small) <= 0.20, "320px must keep at least 80% of width.");
	}

	private static (double W, double H) Crop(string widthField, string heightField)
		=> (Convert.ToDouble(Field(widthField)), Convert.ToDouble(Field(heightField)));

	private static object? Field(string name)
		=> typeof(KeywordImageService).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);

	private static double BoxRatio(int viewport) => (double)viewport / HeroH(viewport);

	private static void AssertRatioEqual(double boxRatio, (double W, double H) crop, double tolerance)
		=> Assert.True(Math.Abs(boxRatio - crop.W / crop.H) <= tolerance,
			$"Box {boxRatio:F3} must equal crop {crop.W}x{crop.H}.");

	private static double WidthDiscard(double boxRatio, (double W, double H) crop)
		=> 1 - boxRatio / (crop.W / crop.H);

	private static double HeightDiscard(double boxRatio, (double W, double H) crop)
		=> 1 - (crop.W / crop.H) / boxRatio;

	private static int HeroH(int viewport)
	{
		var css = FindSiteCss();
		var defaultH = int.Parse(Regex.Match(css, @"--hero-h:\s*(\d+)px").Groups[1].Value);
		var best = (MaxWidth: double.PositiveInfinity, H: defaultH);
		foreach (Match m in Regex.Matches(css, @"@media\s*\(max-width:\s*([\d.]+)px\)\s*\{\s*\.collection-hero--breakout\s*\{\s*--hero-h:\s*(\d+)px;"))
		{
			var maxWidth = double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
			var h = int.Parse(m.Groups[2].Value);
			if (viewport <= maxWidth && maxWidth < best.MaxWidth)
				best = (maxWidth, h);
		}
		return best.H;
	}

	private static string FindSiteCss()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null)
		{
			var candidate = Path.Combine(dir.FullName, "VaultShop.Web", "wwwroot", "css", "site.css");
			if (File.Exists(candidate))
				return File.ReadAllText(candidate);
			dir = dir.Parent;
		}
		throw new FileNotFoundException("Could not locate VaultShop.Web/wwwroot/css/site.css from test output.");
	}
}
