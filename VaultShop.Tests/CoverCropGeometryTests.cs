using System.Reflection;
using System.Text.RegularExpressions;

namespace VaultShop.Web.Tests;

// ponytail: single-ratio guard — variants share the 1905x714 master ratio and the CSS hero
// is fluid (aspect-ratio, no --hero-h rails), so every viewport width shows zero crop.
public class CoverCropGeometryTests
{
	private const double MasterRatio = 1905d / 714;

	[Fact]
	public void CoverVariants_ShareSingleMasterRatio()
	{
		var large = Crop("CoverLargeWidth", "CoverLargeHeight");
		var medium = Crop("CoverMediumWidth", "CoverMediumHeight");
		var small = Crop("CoverSmallWidth", "CoverSmallHeight");

		Assert.Equal((1905d, 714d), large);
		Assert.Equal((1280d, 480d), medium);
		Assert.Equal((768d, 288d), small);

		Assert.True(Math.Abs(medium.W / medium.H - MasterRatio) < 0.002, "Medium must share the master ratio.");
		Assert.True(Math.Abs(small.W / small.H - MasterRatio) < 0.002, "Small must share the master ratio.");
	}

	[Theory]
	[InlineData(320)]
	[InlineData(768)]
	[InlineData(1280)]
	[InlineData(1920)]
	public void FluidHero_ShowsZeroCrop_AtEveryWidth(int viewportWidth)
	{
		// height = 100vw / R, so the box ratio always equals the image ratio — nothing to discard.
		var boxRatio = viewportWidth / (viewportWidth / MasterRatio);
		var large = Crop("CoverLargeWidth", "CoverLargeHeight");

		Assert.True(Math.Abs(boxRatio - large.W / large.H) < 0.001, $"{viewportWidth}px box must equal the master ratio.");
		Assert.True(Math.Abs(HeightDiscard(boxRatio, large)) < 1e-9, $"{viewportWidth}px must discard nothing.");
	}

	[Fact]
	public void HeroCss_IsFluid_WithNoHeightRails()
	{
		var css = FindSiteCss();
		var heroBlock = Regex.Match(css, @"\.collection-hero--breakout\s*\{([^}]*)\}", RegexOptions.Singleline).Groups[1].Value;

		Assert.Contains("aspect-ratio", heroBlock);
		Assert.DoesNotContain("--hero-h", css);
	}

	private static (double W, double H) Crop(string widthField, string heightField)
		=> (Convert.ToDouble(Field(widthField)), Convert.ToDouble(Field(heightField)));

	private static object? Field(string name)
		=> typeof(Web.Services.KeywordImages.KeywordImageService).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);

	private static double HeightDiscard(double boxRatio, (double W, double H) crop)
		=> 1 - (crop.W / crop.H) / boxRatio;

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
