namespace VaultShop.Web.Services.Pricing;

// oferta-huso-horario-ar: admin wall time is Argentina (UTC-3); storage/eval stays UTC.
public static class OfferTimeZone
{
	public const string IanaId = "America/Argentina/Buenos_Aires";
	public const string WindowsId = "Argentina Standard Time";

	private static TimeZoneInfo? _cached;

	public static TimeZoneInfo ArgentinaZone => _cached ??= Resolve();

	public static DateTime? ToUtc(DateTime? value)
		=> value.HasValue
			? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified), ArgentinaZone)
			: null;

	public static DateTime? ToLocal(DateTime? value)
		=> value.HasValue
			? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc), ArgentinaZone)
			: null;

	private static TimeZoneInfo Resolve()
	{
		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById(IanaId);
		}
		catch (TimeZoneNotFoundException)
		{
			return TimeZoneInfo.FindSystemTimeZoneById(WindowsId);
		}
	}
}
