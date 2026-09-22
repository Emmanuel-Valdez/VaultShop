using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using VaultShop.DataAccess.Data;
using VaultShop.Models;

namespace VaultShop.Web.Tests
{
	public class BranchCascadeApiTests
	{
		private static PostalAgency Agency(string code, string name, string provinceCode, string locality,
			string source = "micorreo", string services = "1,40", string hours = "LUN A VIE 9 A 18")
		{
			return new PostalAgency
			{
				Code = code,
				Name = name,
				Street = "CALLE",
				Number = 123,
				Locality = locality,
				City = locality,
				Province = provinceCode,
				ProvinceCode = provinceCode,
				PostalCode = "1000",
				Latitude = -32.9,
				Longitude = -68.8,
				Source = source,
				Services = services,
				Hours = hours,
			};
		}

		private static void Seed(CustomWebApplicationFactory factory)
		{
			using var scope = factory.Services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			db.PostalAgencies.AddRange(
				Agency("M0001", "Mendoza Central", "M", "CAPITAL"),
				Agency("M0002", "Godoy Cruz", "M", "GODOY CRUZ"),
				// homonym locality: same name in two provinces, each with its own branch
				Agency("M0003", "San Martin Mza", "M", "SAN MARTIN"),
				Agency("J0001", "San Martin Sjn", "J", "SAN MARTIN"),
				// non-candidate: unverified gist row without parcel service
				Agency("X0001", "Fantasma", "X", "CAPITAL", source: "gist", services: "1,2,3"));
			db.SaveChanges();
		}

		[Fact]
		public async Task Provinces_ReturnsOnlyProvincesWithCandidates()
		{
			using var factory = new CustomWebApplicationFactory();
			Seed(factory);
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

			var response = await client.GetAsync("/en-US/Customer/Cart/BranchProvinces");
			var body = await response.Content.ReadAsStringAsync();

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			using var json = System.Text.Json.JsonDocument.Parse(body);
			var codes = json.RootElement.EnumerateArray().Select(e => e.GetProperty("code").GetString()).ToList();
			Assert.Equal(["J", "M"], codes);
			Assert.All(json.RootElement.EnumerateArray(),
				e => Assert.False(string.IsNullOrWhiteSpace(e.GetProperty("name").GetString())));
		}

		[Fact]
		public async Task Localities_AreScopedByProvince()
		{
			using var factory = new CustomWebApplicationFactory();
			Seed(factory);
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

			var mendoza = await client.GetStringAsync("/en-US/Customer/Cart/BranchLocalities?provinceCode=M");
			var sanJuan = await client.GetStringAsync("/en-US/Customer/Cart/BranchLocalities?provinceCode=J");
			using var m = System.Text.Json.JsonDocument.Parse(mendoza);
			using var j = System.Text.Json.JsonDocument.Parse(sanJuan);

			Assert.Equal(["CAPITAL", "GODOY CRUZ", "SAN MARTIN"],
				m.RootElement.EnumerateArray().Select(e => e.GetString()).ToList());
			Assert.Equal(["SAN MARTIN"],
				j.RootElement.EnumerateArray().Select(e => e.GetString()).ToList());
		}

		[Fact]
		public async Task Branches_ReturnHomonymLocalityOnlyWithinProvince_WithHours()
		{
			using var factory = new CustomWebApplicationFactory();
			Seed(factory);
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

			var response = await client.GetAsync("/en-US/Customer/Cart/BranchBranches?provinceCode=M&locality=SAN%20MARTIN");
			var body = await response.Content.ReadAsStringAsync();

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			using var json = System.Text.Json.JsonDocument.Parse(body);
			var items = json.RootElement.EnumerateArray().ToList();
			var item = Assert.Single(items);
			Assert.Equal("M0003", item.GetProperty("code").GetString());
			Assert.Equal("LUN A VIE 9 A 18", item.GetProperty("hours").GetString());
			Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("address").GetString()));
		}

		[Fact]
		public async Task Branches_ExcludesNonCandidates_And_RejectsMissingParams()
		{
			using var factory = new CustomWebApplicationFactory();
			Seed(factory);
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

			var response = await client.GetAsync("/en-US/Customer/Cart/BranchBranches?provinceCode=X&locality=CAPITAL");
			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
			Assert.Empty(json.RootElement.EnumerateArray());

			Assert.Equal(HttpStatusCode.BadRequest,
				(await client.GetAsync("/en-US/Customer/Cart/BranchBranches?provinceCode=M")).StatusCode);
			Assert.Equal(HttpStatusCode.BadRequest,
				(await client.GetAsync("/en-US/Customer/Cart/BranchLocalities")).StatusCode);
		}
	}
}
