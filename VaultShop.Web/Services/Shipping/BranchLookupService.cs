using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using VaultShop.DataAccess.Data;
using VaultShop.Models;
using VaultShop.Utility;

namespace VaultShop.Web.Services.Shipping;

public interface IBranchLookupService
{
	PostalAgency? GetByCode(string? code);
	IReadOnlyList<(string Code, string Name)> GetCandidateProvinces();
	IReadOnlyList<string> GetCandidateLocalities(string provinceCode);
	IReadOnlyList<PostalAgency> GetCandidateBranches(string provinceCode, string locality);
}

public sealed class BranchLookupService : IBranchLookupService
{
	private readonly ApplicationDbContext _db;

	public BranchLookupService(ApplicationDbContext db)
	{
		_db = db;
	}

	public PostalAgency? GetByCode(string? code)
	{
		if (string.IsNullOrWhiteSpace(code))
		{
			return null;
		}

		var trimmed = code.Trim();
		// ponytail: eligibility lives in IsCandidateExpr — a forged code for an unverified or non-parcel branch resolves to null in the same SQL.
		return _db.PostalAgencies.AsNoTracking()
			.Where(IsCandidateExpr)
			.FirstOrDefault(a => a.Code == trimmed);
	}

	// ponytail: cascade queries stay IQueryable until ToList — filtering happens in SQL (no full-table load); the (ProvinceCode, Locality) covering index serves all three.
	public IReadOnlyList<(string Code, string Name)> GetCandidateProvinces()
	{
		var codes = _db.PostalAgencies.AsNoTracking()
			.Where(IsCandidateExpr)
			.Select(a => a.ProvinceCode)
			.Distinct()
			.ToList();
		var names = SD.CorreoProvinces.ToDictionary(p => p.Code, p => p.Name);
		return codes
			.Where(names.ContainsKey)
			.Select(c => (c, names[c]))
			.OrderBy(p => p.c)
			.ToList();
	}

	public IReadOnlyList<string> GetCandidateLocalities(string provinceCode)
	{
		if (string.IsNullOrWhiteSpace(provinceCode))
		{
			return [];
		}

		var code = provinceCode.Trim().ToUpperInvariant();
		return _db.PostalAgencies.AsNoTracking()
			.Where(IsCandidateExpr)
			.Where(a => a.ProvinceCode == code)
			.Select(a => a.Locality)
			.Distinct()
			.OrderBy(l => l)
			.ToList();
	}

	public IReadOnlyList<PostalAgency> GetCandidateBranches(string provinceCode, string locality)
	{
		if (string.IsNullOrWhiteSpace(provinceCode) || string.IsNullOrWhiteSpace(locality))
		{
			return [];
		}

		// ponytail: locality is always scoped by province — same-name localities in different provinces never leak across.
		var code = provinceCode.Trim().ToUpperInvariant();
		var place = locality.Trim().ToLower();
		return _db.PostalAgencies.AsNoTracking()
			.Where(IsCandidateExpr)
			.Where(a => a.ProvinceCode == code && a.Locality.ToLower() == place)
			.OrderBy(a => a.Name)
			.ThenBy(a => a.Code)
			.ToList();
	}

	// ponytail: the single eligibility predicate — SQL-translatable (LOWER/REPLACE/LIKE only),
	// reused by GetByCode and every cascade query so listings never show branches the POST rejects.
	// MiCorreo valida elegibilidad de paqueteria por si misma — una fila
	// source=micorreo no necesita servicio 40 del scrapeo del sitio.
	internal static readonly Expression<Func<PostalAgency, bool>> IsCandidateExpr = a =>
		(a.Source != null && a.Source.ToLower() == "correo" &&
			("," + (a.Services ?? "").Replace(";", ",").Replace("|", ",").Replace(" ", ",") + ",").Contains(",40,"))
		|| (a.Source != null && a.Source.ToLower() == "micorreo");

	private static readonly Func<PostalAgency, bool> IsCandidateFunc = IsCandidateExpr.Compile();

	internal static bool IsCandidate(PostalAgency a) => IsCandidateFunc(a);
}
