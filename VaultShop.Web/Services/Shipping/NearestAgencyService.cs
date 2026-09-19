using Microsoft.EntityFrameworkCore;
using VaultShop.DataAccess.Data;
using VaultShop.Models;

namespace VaultShop.Web.Services.Shipping;

public sealed record AgencyCandidate(string Code, string Name, string Address, string Locality, string Province, double? DistanceKm);

public interface INearestAgencyService
{
	IReadOnlyList<AgencyCandidate> FindNearest(double? lat, double? lon, string? province, string? locality, int max = 5);
	PostalAgency? GetByCode(string? code);
}

public sealed class NearestAgencyService : INearestAgencyService
{
	private const double EarthRadiusKm = 6371.0;
	private readonly ApplicationDbContext _db;

	public NearestAgencyService(ApplicationDbContext db)
	{
		_db = db;
	}

	public IReadOnlyList<AgencyCandidate> FindNearest(double? lat, double? lon, string? province, string? locality, int max = 5)
	{
		var agencies = _db.PostalAgencies.AsNoTracking().ToList();
		return Rank(agencies, lat, lon, province, locality, max);
	}

	public PostalAgency? GetByCode(string? code)
	{
		if (string.IsNullOrWhiteSpace(code))
		{
			return null;
		}

		var agency = _db.PostalAgencies.AsNoTracking().FirstOrDefault(a => a.Code == code.Trim());
		// ponytail: same candidate rules as Rank — a forged code for an unverified or non-parcel branch resolves to null.
		if (agency is null || !string.Equals(agency.Source, "correo", StringComparison.OrdinalIgnoreCase) || !HasParcelService(agency.Services))
		{
			return null;
		}

		return agency;
	}

	internal static IReadOnlyList<AgencyCandidate> Rank(IEnumerable<PostalAgency> agencies, double? lat, double? lon, string? province, string? locality, int max = 5)
	{
		var candidates = agencies.Where(a => string.Equals(a.Source, "correo", StringComparison.OrdinalIgnoreCase) && HasParcelService(a.Services)).ToList();
		var inProvince = MatchProvince(candidates, province);
		// ponytail: province mismatch broadens to national top-5 instead of returning empty.
		var pool = inProvince.Count > 0 ? inProvince : candidates;

		if (lat.HasValue && lon.HasValue)
		{
			return pool
				.Select(a => ToCandidate(a, HaversineKm(lat.Value, lon.Value, a.Latitude, a.Longitude)))
				.OrderBy(c => c.DistanceKm)
				.ThenBy(c => c.Code, StringComparer.Ordinal)
				.Take(Math.Max(1, max))
				.ToList();
		}

		var inLocality = MatchLocality(pool, locality);
		var fallback = inLocality.Count > 0 ? inLocality : pool;
		return fallback
			.Select(a => ToCandidate(a, null))
			.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
			.ThenBy(c => c.Code, StringComparer.Ordinal)
			.Take(Math.Max(1, max))
			.ToList();
	}

	internal static bool HasParcelService(string? services)
	{
		if (string.IsNullOrWhiteSpace(services))
		{
			return false;
		}

		return services.Split([',', ';', '|', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Any(part => part == "40");
	}

	internal static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
	{
		var dLat = ToRadians(lat2 - lat1);
		var dLon = ToRadians(lon2 - lon1);
		var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
			+ Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
			* Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
		return 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(a));
	}

	private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

	private static List<PostalAgency> MatchProvince(List<PostalAgency> agencies, string? province)
	{
		if (string.IsNullOrWhiteSpace(province))
		{
			return [];
		}

		return agencies.Where(a =>
			string.Equals(a.Province, province.Trim(), StringComparison.OrdinalIgnoreCase) ||
			string.Equals(a.ProvinceCode, province.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
	}

	private static List<PostalAgency> MatchLocality(List<PostalAgency> agencies, string? locality)
	{
		if (string.IsNullOrWhiteSpace(locality))
		{
			return [];
		}

		return agencies.Where(a => string.Equals(a.Locality, locality.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
	}

	private static AgencyCandidate ToCandidate(PostalAgency agency, double? distanceKm)
	{
		var street = agency.Number.HasValue ? $"{agency.Street} {agency.Number}".Trim() : agency.Street.Trim();
		return new AgencyCandidate(agency.Code, agency.Name, street, agency.Locality, agency.Province, distanceKm);
	}
}
