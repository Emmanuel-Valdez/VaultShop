using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using VaultShop.Web.Services.Shipping;

namespace VaultShop.Web.Tests
{
	public class GeorefAddressServiceTests
	{
		[Fact]
		public async Task GeocodeAsync_KnownAddress_ReturnsLatLon()
		{
			var handler = new StubHttpMessageHandler((request, _) =>
			{
				var query = request.RequestUri?.Query ?? "";
				Assert.Contains("direccion=San%20Martin%20123", query);
				Assert.Contains("provincia=Mendoza", query);
				Assert.Contains("localidad=Godoy%20Cruz", query);
				Assert.Contains("max=1", query);
				return new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent(
						"{\"direcciones\":[{\"ubicacion\":{\"lat\":-32.925,\"lon\":-68.845}}]}",
						Encoding.UTF8, "application/json")
				};
			});

			var service = CreateService(handler);

			var result = await service.GeocodeAsync("San Martin 123", "Mendoza", "Godoy Cruz");

			Assert.NotNull(result);
			Assert.Equal(-32.925, result.Value.Lat, 3);
			Assert.Equal(-68.845, result.Value.Lon, 3);
		}

		[Fact]
		public async Task GeocodeAsync_NoMatch_ReturnsNull()
		{
			var handler = new StubHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("{\"direcciones\":[]}", Encoding.UTF8, "application/json")
			});

			var result = await CreateService(handler).GeocodeAsync("Calle Inexistente 9999", "Mendoza", null);

			Assert.Null(result);
		}

		[Fact]
		public async Task GeocodeAsync_NonSuccessStatus_ReturnsNull()
		{
			var handler = new StubHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.BadGateway));

			var result = await CreateService(handler).GeocodeAsync("San Martin 123", "Mendoza", null);

			Assert.Null(result);
		}

		[Fact]
		public async Task GeocodeAsync_MissingUbicacion_ReturnsNull()
		{
			var handler = new StubHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("{\"direcciones\":[{\"nomenclatura\":\"X\"}]}", Encoding.UTF8, "application/json")
			});

			var result = await CreateService(handler).GeocodeAsync("San Martin 123", "Mendoza", null);

			Assert.Null(result);
		}

		private static GeorefAddressService CreateService(StubHttpMessageHandler handler)
		{
			var client = new HttpClient(handler) { BaseAddress = new Uri("https://apis.datos.gob.ar/georef/api/") };
			return new GeorefAddressService(client, NullLogger<GeorefAddressService>.Instance);
		}
	}
}
