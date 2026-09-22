using VaultShop.Models;
using VaultShop.Web.Services.Shipping;

namespace VaultShop.Web.Tests
{
	// ponytail: ranking is gone (cascade replaced nearest search) — these pin the single
	// eligibility predicate every listing and POST shares.
	public class BranchLookupServiceTests
	{
		[Fact]
		public void IsCandidate_ExcludesUnverifiedRows()
		{
			var verified = Agency("M1000", "Verificada");
			var gistOnly = Agency("M1001", "Gist");
			gistOnly.Source = "gist";

			Assert.True(BranchLookupService.IsCandidate(verified));
			Assert.False(BranchLookupService.IsCandidate(gistOnly));
		}

		[Fact]
		public void IsCandidate_ExcludesBranchWithoutParcelService()
		{
			var parcel = Agency("M1000", "Parcel");
			var noParcel = Agency("M1001", "Obelisco", services: "1,2,3");

			Assert.True(BranchLookupService.IsCandidate(parcel));
			Assert.False(BranchLookupService.IsCandidate(noParcel));
		}

		[Fact]
		public void IsCandidate_IncludesVerifiedUnidadPostal()
		{
			var up = Agency("UP-M-abc123", "UP Punto", kind: "UP");

			Assert.True(BranchLookupService.IsCandidate(up));
		}

		[Fact]
		public void IsCandidate_IncludesMicorreoWithoutParcelService()
		{
			var micorreo = Agency("V0004", "Ushuaia", services: "1,2,3");
			micorreo.Source = "micorreo";

			Assert.True(BranchLookupService.IsCandidate(micorreo));
		}

		private static PostalAgency Agency(string code, string name, string services = "1,40", string kind = "SUCURSAL")
		{
			return new PostalAgency
			{
				Code = code,
				Name = name,
				Street = "CALLE",
				Number = 123,
				Locality = "CAPITAL",
				City = "CAPITAL",
				Province = "Mendoza",
				ProvinceCode = "M",
				PostalCode = "M5500",
				Latitude = -32.89,
				Longitude = -68.84,
				Source = "correo",
				Services = services,
				Kind = kind
			};
		}
	}
}
