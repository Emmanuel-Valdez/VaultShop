using System.Text.Json;

namespace VaultShop.Web.Services.Shipping;

public sealed class GeorefOptions
{
	public string BaseUrl { get; set; } = "https://apis.datos.gob.ar/georef/api/";
}

public interface IGeorefAddressService
{
	Task<(double Lat, double Lon)?> GeocodeAsync(string streetAddress, string? province, string? locality, CancellationToken ct = default);
}

public sealed class GeorefAddressService : IGeorefAddressService
{
	private readonly HttpClient _http;
	private readonly ILogger<GeorefAddressService> _logger;

	public GeorefAddressService(HttpClient http, ILogger<GeorefAddressService> logger)
	{
		_http = http;
		_logger = logger;
	}

	public async Task<(double Lat, double Lon)?> GeocodeAsync(string streetAddress, string? province, string? locality, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(streetAddress))
		{
			return null;
		}

		try
		{
			var url = "direcciones?direccion=" + Uri.EscapeDataString(streetAddress.Trim())
				+ (string.IsNullOrWhiteSpace(province) ? "" : "&provincia=" + Uri.EscapeDataString(province.Trim()))
				+ (string.IsNullOrWhiteSpace(locality) ? "" : "&localidad=" + Uri.EscapeDataString(locality.Trim()))
				+ "&max=1";
			using var response = await _http.GetAsync(url, ct);
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogWarning("Georef lookup failed with status {StatusCode}.", (int)response.StatusCode);
				return null;
			}

			using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
			if (!json.RootElement.TryGetProperty("direcciones", out var addresses) || addresses.GetArrayLength() == 0)
			{
				return null;
			}

			var first = addresses[0];
			if (!first.TryGetProperty("ubicacion", out var location))
			{
				return null;
			}

			if (!TryGetDouble(location, "lat", out var lat))
			{
				return null;
			}

			if (!TryGetDouble(location, "lon", out var lon) && !TryGetDouble(location, "lng", out lon))
			{
				return null;
			}

			return (lat, lon);
		}
		catch (HttpRequestException ex)
		{
			_logger.LogWarning(ex, "Georef lookup request failed.");
			return null;
		}
		catch (TaskCanceledException ex)
		{
			_logger.LogWarning(ex, "Georef lookup timed out.");
			return null;
		}
		catch (JsonException ex)
		{
			_logger.LogDebug(ex, "Georef lookup returned an unparseable response.");
			return null;
		}
	}

	private static bool TryGetDouble(JsonElement element, string name, out double value)
	{
		value = 0;
		if (!element.TryGetProperty(name, out var property))
		{
			return false;
		}

		if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out value))
		{
			return true;
		}

		return property.ValueKind == JsonValueKind.String
			&& double.TryParse(property.GetString(), global::System.Globalization.NumberStyles.Float, global::System.Globalization.CultureInfo.InvariantCulture, out value);
	}
}
