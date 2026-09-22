using System.Text.Json;
using VaultShop.Models;
using VaultShop.Utility;

namespace VaultShop.Web.Tests
{
	public class CorreoProvincesTests
	{
		[Fact]
		public void CorreoProvinces_Has24Codes()
		{
			Assert.Equal(24, SD.CorreoProvinces.Count);
			Assert.Equal(24, SD.CorreoProvinces.Select(p => p.Code).Distinct().Count());
		}

		[Fact]
		public void SeedProvinceCodes_AreSubsetOfStaticList()
		{
			var json = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
				"..", "..", "..", "..", "VaultShop.DataAccess", "SeedData", "sucursales.json")));
			var rows = JsonSerializer.Deserialize<List<PostalAgency>>(json,
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
			var known = SD.CorreoProvinces.Select(p => p.Code).ToHashSet();

			Assert.All(rows, r => Assert.Contains(r.ProvinceCode, known));
		}

		[Fact]
		public void UnknownProvinceCode_IsNotInStaticList()
		{
			Assert.DoesNotContain("I", SD.CorreoProvinces.Select(p => p.Code));
			Assert.DoesNotContain("Ñ", SD.CorreoProvinces.Select(p => p.Code));
			Assert.DoesNotContain("O", SD.CorreoProvinces.Select(p => p.Code));
			Assert.DoesNotContain("ZZ", SD.CorreoProvinces.Select(p => p.Code));
		}
	}
}
