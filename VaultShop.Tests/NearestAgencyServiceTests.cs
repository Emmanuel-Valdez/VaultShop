using VaultShop.Models;
using VaultShop.Web.Services.Shipping;

namespace VaultShop.Web.Tests
{
	public class NearestAgencyServiceTests
	{
		[Fact]
		public void Rank_OrdersByDistanceWithinProvince()
		{
			var agencies = new[]
			{
				Agency("M1000", "Lejos", "Mendoza", -32.95, -68.85),
				Agency("M1001", "Cerca", "Mendoza", -32.89, -68.84),
				Agency("M1002", "Media", "Mendoza", -32.92, -68.84),
			};

			var result = NearestAgencyService.Rank(agencies, -32.89, -68.84, "Mendoza", null);

			Assert.Equal(3, result.Count);
			Assert.Equal("M1001", result[0].Code);
			Assert.Equal("M1002", result[1].Code);
			Assert.Equal("M1000", result[2].Code);
			Assert.All(result, c => Assert.NotNull(c.DistanceKm));
			Assert.True(result[0].DistanceKm <= result[1].DistanceKm);
		}

		[Fact]
		public void Rank_Tie_BreaksByCode()
		{
			var agencies = new[]
			{
				Agency("M1001", "B", "Mendoza", -32.89, -68.84),
				Agency("M1000", "A", "Mendoza", -32.89, -68.84),
			};

			var result = NearestAgencyService.Rank(agencies, -32.89, -68.84, "Mendoza", null);

			Assert.Equal("M1000", result[0].Code);
			Assert.Equal("M1001", result[1].Code);
		}

		[Fact]
		public void Rank_FewerThanFiveInProvince_ReturnsAll()
		{
			var agencies = new[]
			{
				Agency("M1000", "Una", "Mendoza", -32.89, -68.84),
				Agency("M1001", "Dos", "Mendoza", -32.90, -68.85),
			};

			var result = NearestAgencyService.Rank(agencies, -32.89, -68.84, "Mendoza", null);

			Assert.Equal(2, result.Count);
		}

		[Fact]
		public void Rank_ExcludesUnverifiedRows()
		{
			var verified = Agency("M1000", "Verificada", "Mendoza", -32.89, -68.84);
			var gistOnly = Agency("M1001", "Gist", "Mendoza", -32.89, -68.84);
			gistOnly.Source = "gist";

			var result = NearestAgencyService.Rank([verified, gistOnly], -32.89, -68.84, "Mendoza", null);

			Assert.Single(result);
			Assert.Equal("M1000", result[0].Code);
		}

		[Fact]
		public void Rank_ExcludesBranchWithoutParcelService()
		{
			var parcel = Agency("M1000", "Parcel", "Mendoza", -32.89, -68.84);
			var noParcel = Agency("M1001", "Obelisco", "Mendoza", -32.89, -68.84, services: "1,2,3");

			var result = NearestAgencyService.Rank([parcel, noParcel], -32.89, -68.84, "Mendoza", null);

			Assert.Single(result);
			Assert.Equal("M1000", result[0].Code);
		}

		[Fact]
		public void Rank_IncludesVerifiedUnidadPostal()
		{
			var up = Agency("UP-M-abc123", "UP Punto", "Mendoza", -32.89, -68.84, kind: "UP");

			var result = NearestAgencyService.Rank([up], -32.89, -68.84, "Mendoza", null);

			Assert.Single(result);
			Assert.Equal("UP-M-abc123", result[0].Code);
		}

		[Fact]
		public void Rank_ProvinceMismatch_BroadensToNational()
		{
			var agencies = new[] { Agency("C1000", "Cordoba", "Córdoba", -31.42, -64.18) };

			var result = NearestAgencyService.Rank(agencies, -31.42, -64.18, "Mendoza", null);

			Assert.Single(result);
			Assert.Equal("C1000", result[0].Code);
		}

		[Fact]
		public void Rank_NoCoordinates_ReturnsProvinceAlphabeticalWithoutDistance()
		{
			var agencies = new[]
			{
				Agency("M1001", "Zeta", "Mendoza", -32.89, -68.84),
				Agency("M1000", "Alfa", "Mendoza", -32.90, -68.85),
			};

			var result = NearestAgencyService.Rank(agencies, null, null, "Mendoza", null);

			Assert.Equal("M1000", result[0].Code);
			Assert.Equal("M1001", result[1].Code);
			Assert.All(result, c => Assert.Null(c.DistanceKm));
		}

		private static PostalAgency Agency(string code, string name, string province, double lat, double lon, string services = "1,40", string kind = "SUCURSAL")
		{
			return new PostalAgency
			{
				Code = code,
				Name = name,
				Street = "CALLE",
				Number = 123,
				Locality = "CAPITAL",
				City = "CAPITAL",
				Province = province,
				ProvinceCode = "M",
				PostalCode = "M5500",
				Latitude = lat,
				Longitude = lon,
				Source = "correo",
				Services = services,
				Kind = kind
			};
		}
	}
}
