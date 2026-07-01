namespace UkiyoDesignsWeb.Services.Branding;

public sealed class ThemeOptions
{
	public const string DefaultPrimary = "#1a2a4a";
	public const string DefaultPrimaryDark = "#0d1625";
	public const string DefaultAccent = "#d4a017";
	public const string DefaultSurface = "#f7f8fa";
	public const string DefaultSurfaceDark = "#1e2030";

	public string Primary { get; set; } = DefaultPrimary;

	public string PrimaryDark { get; set; } = DefaultPrimaryDark;

	public string Accent { get; set; } = DefaultAccent;

	public string Surface { get; set; } = DefaultSurface;

	public string SurfaceDark { get; set; } = DefaultSurfaceDark;

	public string PrimaryCss => NormalizeHexColor(Primary, DefaultPrimary);

	public string PrimaryDarkCss => NormalizeHexColor(PrimaryDark, DefaultPrimaryDark);

	public string AccentCss => NormalizeHexColor(Accent, DefaultAccent);

	public string SurfaceCss => NormalizeHexColor(Surface, DefaultSurface);

	public string SurfaceDarkCss => NormalizeHexColor(SurfaceDark, DefaultSurfaceDark);

	public string PrimaryRgb => ToRgb(PrimaryCss);

	public string PrimaryDarkRgb => ToRgb(PrimaryDarkCss);

	public string AccentRgb => ToRgb(AccentCss);

	public string SurfaceRgb => ToRgb(SurfaceCss);

	public static string NormalizeHexColor(string? value, string fallback)
	{
		var color = value?.Trim();

		if (!IsHexColor(color))
		{
			color = fallback;
		}

		if (color!.Length == 4)
		{
			return string.Concat(
				'#',
				color[1], color[1],
				color[2], color[2],
				color[3], color[3]).ToLowerInvariant();
		}

		return color.ToLowerInvariant();
	}

	private static bool IsHexColor(string? value)
	{
		if (string.IsNullOrWhiteSpace(value) || value[0] != '#')
		{
			return false;
		}

		if (value.Length is not (4 or 7 or 9))
		{
			return false;
		}

		for (var i = 1; i < value.Length; i++)
		{
			if (!Uri.IsHexDigit(value[i]))
			{
				return false;
			}
		}

		return true;
	}

	private static string ToRgb(string cssColor)
	{
		var red = Convert.ToInt32(cssColor.Substring(1, 2), 16);
		var green = Convert.ToInt32(cssColor.Substring(3, 2), 16);
		var blue = Convert.ToInt32(cssColor.Substring(5, 2), 16);

		return $"{red}, {green}, {blue}";
	}
}
