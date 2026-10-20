using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VaultShop.DataAccess.Data;
using VaultShop.Models;
using VaultShop.Utility;
using VaultShop.Web.Services.Checkout;
using VaultShop.Web.Services.Pricing;

namespace VaultShop.Web.Tests
{
	// envio-gratis-o-cargo §4 — domicile cascade, modality rendering, dynamic totals, persistence.
	public class CheckoutDeliveryModalityTests
	{
		private static readonly (string Code, string Name, string Locality, string ProvinceCode)[] Agencies =
		[
			("TEST01", "Godoy Cruz Branch", "Godoy Cruz", "M"),
			("TEST02", "Maipu Branch", "Maipú", "M"),
			("TEST03", "La Plata Branch", "La Plata", "B"),
		];

		private static void SeedPostalAgencies(WebApplicationFactory<Program> factory)
		{
			using var scope = factory.Services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var index = 1;
			foreach (var agency in Agencies)
			{
				if (db.PostalAgencies.Any(a => a.Code == agency.Code))
				{
					continue;
				}

				db.PostalAgencies.Add(new PostalAgency
				{
					Code = agency.Code,
					Name = agency.Name,
					Street = "Calle",
					Number = index++,
					Locality = agency.Locality,
					City = agency.Locality,
					Province = agency.ProvinceCode,
					ProvinceCode = agency.ProvinceCode,
					PostalCode = "M5500",
					Latitude = -32.9,
					Longitude = -68.8,
					Source = "correo",
					Services = "40",
					Kind = "SUCURSAL",
					Hours = "LUN A VIE 9 A 18",
				});
			}
			db.SaveChanges();
		}

		private static void SeedCart(WebApplicationFactory<Program> factory, string email, bool freeShipping, decimal retailPrice = 100m, decimal wholesalePrice = 70m)
		{
			using var scope = factory.Services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var user = db.ApplicationUsers.Single(u => u.Email == email);
			var category = new Category { Name = "Modality Category", AvgShippingCost = 100m };
			var product = new Product
			{
				Name = freeShipping ? "Free Product" : "Paid Product",
				Description = "Product for delivery modality tests.",
				MaxExpectation = 10,
				Category = category,
				ListPrice = retailPrice,
				FinalRetailPrice = retailPrice,
				FinalWholesalePrice = wholesalePrice,
				IsAvailableInStore = true,
				IsDeleted = false,
				StockQuantity = 100,
				FreeShipping = freeShipping,
			};
			db.Categories.Add(category);
			db.Products.Add(product);
			db.ShoppingCarts.Add(new ShoppingCart { ApplicationUserId = user.Id, Product = product, Count = 1 });
			db.SaveChanges();
		}

		private static void SeedProfileDomicile(WebApplicationFactory<Program> factory, string email, string state, string city)
		{
			using var scope = factory.Services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var user = db.ApplicationUsers.Single(u => u.Email == email);
			user.State = state;
			user.City = city;
			db.SaveChanges();
		}

		private static async Task<List<string>> GetModalityCodesAsync(HttpClient client, string provinceCode, string locality)
		{
			var json = await client.GetStringAsync($"/en-US/Customer/Cart/DeliveryModalities?provinceCode={provinceCode}&locality={Uri.EscapeDataString(locality)}");
			using var document = JsonDocument.Parse(json);
			return document.RootElement.EnumerateArray().Select(e => e.GetProperty("code").GetString()!).ToList();
		}

		private static async Task<string> PostSummaryHtmlAsync(HttpClient client, string token, string state, string locality, string? deliveryType, string? pickupCode)
		{
			var response = await PostSummaryHtmlToResponseAsync(client, token, state, locality, deliveryType, pickupCode);
			return await response.Content.ReadAsStringAsync();
		}

		[Fact]
		public async Task DeliveryModalities_GodoyCruzPaid_OffersMotorcycleVendorAndCorreo()
		{
			using var factory = new CustomWebApplicationFactory();
			SeedPostalAgencies(factory);
			SeedCart(factory, factory.CustomerEmail, freeShipping: false);
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
			await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

			var codes = await GetModalityCodesAsync(client, "M", "Godoy Cruz");

			Assert.Equal([SD.DeliveryTypeMotorcycle, SD.DeliveryTypeVendorPickup, SD.DeliveryTypePickup], codes);
		}

		[Fact]
		public async Task DeliveryModalities_GodoyCruzFree_OffersMotorcycleAndCorreoWithoutVendor()
		{
			using var factory = new CustomWebApplicationFactory();
			SeedPostalAgencies(factory);
			SeedCart(factory, factory.CustomerEmail, freeShipping: true);
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
			await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

			var codes = await GetModalityCodesAsync(client, "M", "Godoy Cruz");

			Assert.Equal([SD.DeliveryTypeMotorcycle, SD.DeliveryTypePickup], codes);
		}

		[Fact]
		public async Task DeliveryModalities_NonGranMendoza_OffersCorreoOnly()
		{
			using var factory = new CustomWebApplicationFactory();
			SeedPostalAgencies(factory);
			SeedCart(factory, factory.CustomerEmail, freeShipping: false);
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
			await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

			var codes = await GetModalityCodesAsync(client, "B", "La Plata");

			Assert.Equal([SD.DeliveryTypePickup], codes);
		}

		[Fact]
		public async Task DeliveryModalities_UnknownLocality_ReturnsBadRequest()
		{
			using var factory = new CustomWebApplicationFactory();
			SeedPostalAgencies(factory);
			SeedCart(factory, factory.CustomerEmail, freeShipping: false);
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
			await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

			var response = await client.GetAsync("/en-US/Customer/Cart/DeliveryModalities?provinceCode=M&locality=NOWHERE");

			Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		}

		[Fact]
		public async Task Summary_WithProfileDomicile_ScopesLocalitiesToProvince()
		{
			using var factory = new CustomWebApplicationFactory();
			SeedPostalAgencies(factory);
			SeedCart(factory, factory.CustomerEmail, freeShipping: false);
			SeedProfileDomicile(factory, factory.CustomerEmail, "Mendoza", "Godoy Cruz");
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
			await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

			var html = await client.GetStringAsync("/en-US/Customer/Cart/Summary");

			Assert.Contains("data-code=\"M\" selected", html);
			Assert.Contains("value=\"Godoy Cruz\" selected", html);
			Assert.Contains("Maip", html);
			Assert.DoesNotContain("La Plata", html);
		}

		[Theory]
		[InlineData("es-AR", "Envío en moto a domicilio", "Retiro a coordinar con el vendedor", "Gratis")]
		[InlineData("en-US", "Motorcycle delivery to your home", "Pickup to coordinate with the seller", "Free")]
		public async Task Summary_GodoyCruz_RendersLocalizedDeliveryCardsAndArgentinaPickupWindow(
			string culture, string motorcycleLabel, string vendorPickupLabel, string freeLabel)
		{
			using var factory = new CustomWebApplicationFactory();
			SeedPostalAgencies(factory);
			SeedCart(factory, factory.CustomerEmail, freeShipping: false);
			SeedProfileDomicile(factory, factory.CustomerEmail, "Mendoza", "Godoy Cruz");
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
			await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

			var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/{culture}/Customer/Cart/Summary"));
			var todayInArgentina = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, OfferTimeZone.ArgentinaZone));
			var dateCulture = CultureInfo.GetCultureInfo(culture);
			var hasCurrentPickupWindow = Enumerable.Range(-1, 2).Any(dayOffset =>
			{
				var start = todayInArgentina.AddDays(dayOffset);
				return html.Contains(start.ToString("d", dateCulture))
					&& html.Contains(start.AddDays(7).ToString("d", dateCulture));
			});

			Assert.Contains("checkout-delivery-option", html);
			Assert.Contains("role=\"radiogroup\"", html);
			Assert.Contains(motorcycleLabel, html);
			Assert.Contains(vendorPickupLabel, html);
			Assert.True(hasCurrentPickupWindow, "Expected the pickup dates to span today in Argentina through seven days later.");
			Assert.Contains(freeLabel, html);
		}

		[Fact]
		public async Task SummaryPost_UnknownDomicilePair_RejectedWithoutOrder()
		{
			using var factory = new CustomWebApplicationFactory();
			SeedPostalAgencies(factory);
			SeedCart(factory, factory.CustomerEmail, freeShipping: false);
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
			await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);
			var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Customer/Cart/Summary");

			var html = await PostSummaryHtmlAsync(client, token, "Mendoza", "Not A Catalog Locality", deliveryType: null, pickupCode: "TEST01");

			Assert.Contains("Select a province and locality from the list.", html);
			using var scope = factory.Services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			Assert.Empty(db.OrderHeaders.AsNoTracking());
		}

		[Fact]
		public async Task SummaryPost_GodoyCruzPaid_RendersAllModalities()
		{
			using var factory = new CustomWebApplicationFactory();
			SeedPostalAgencies(factory);
			SeedCart(factory, factory.CustomerEmail, freeShipping: false);
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
			await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);
			var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Customer/Cart/Summary");

			// No branch posted: the failed POST re-renders the summary with modality radios for the locality.
			var html = await PostSummaryHtmlAsync(client, token, "Mendoza", "Godoy Cruz", deliveryType: null, pickupCode: null);

			Assert.Contains("Motorcycle delivery to your home", html);
			Assert.Contains("Pickup to coordinate with the seller", html);
			Assert.Contains("Correo pickup", html);
		}

		[Fact]
		public async Task SummaryPost_GodoyCruzFree_HidesVendorPickup()
		{
			using var factory = new CustomWebApplicationFactory();
			SeedPostalAgencies(factory);
			SeedCart(factory, factory.CustomerEmail, freeShipping: true);
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
			await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);
			var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Customer/Cart/Summary");

			var html = await PostSummaryHtmlAsync(client, token, "Mendoza", "Godoy Cruz", deliveryType: null, pickupCode: null);

			Assert.Contains("Motorcycle delivery to your home", html);
			Assert.Contains("Correo pickup", html);
			Assert.DoesNotContain("Pickup to coordinate with the seller", html);
		}

		[Fact]
		public async Task SummaryPost_NonGranMendoza_RendersCorreoOnly()
		{
			using var factory = new CustomWebApplicationFactory();
			SeedPostalAgencies(factory);
			SeedCart(factory, factory.CustomerEmail, freeShipping: false);
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
			await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);
			var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Customer/Cart/Summary");

			var html = await PostSummaryHtmlAsync(client, token, "Buenos Aires", "La Plata", deliveryType: null, pickupCode: null);

			Assert.Contains("Correo pickup", html);
			Assert.DoesNotContain("Motorcycle delivery to your home", html);
			Assert.DoesNotContain("Pickup to coordinate with the seller", html);
		}

		[Fact]
		public async Task SummaryTotals_VendorPickupRemovesCharge_CorreoKeepsIt()
		{
			using var factory = new CustomWebApplicationFactory();
			SeedPostalAgencies(factory);
			SeedCart(factory, factory.CustomerEmail, freeShipping: false);
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
			await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

			var vendor = await client.GetStringAsync("/en-US/Customer/Cart/SummaryTotals?paymentMethod=BankTransfer&modality=V");
			var correo = await client.GetStringAsync("/en-US/Customer/Cart/SummaryTotals?paymentMethod=BankTransfer&modality=S");

			Assert.Contains("\"hasShippingCharge\":false", vendor);
			// Product 100, no charge for vendor pickup.
			Assert.Contains("\"orderTotal\":\"$100.00\"", vendor);
			Assert.Contains("\"hasShippingCharge\":true", correo);
			// Product 100 + untouched charge 100.
			Assert.Contains("\"orderTotal\":\"$200.00\"", correo);
		}

		[Fact]
		public async Task SummaryTotals_FreeCart_ShowsFreeShippingWithoutCharge()
		{
			using var factory = new CustomWebApplicationFactory();
			SeedPostalAgencies(factory);
			SeedCart(factory, factory.CustomerEmail, freeShipping: true);
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
			await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

			var json = await client.GetStringAsync("/en-US/Customer/Cart/SummaryTotals?paymentMethod=BankTransfer&modality=S");

			Assert.Contains("\"isFreeShipping\":true", json);
			Assert.Contains("\"hasShippingCharge\":false", json);
			Assert.Contains("\"orderTotal\":\"$100.00\"", json);
		}

		[Fact]
		public async Task SummaryPost_VendorPickup_PersistsNoBranchZeroChargeAndWording()
		{
			using var factory = new CustomWebApplicationFactory();
			SeedPostalAgencies(factory);
			SeedCart(factory, factory.CustomerEmail, freeShipping: false);
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
			await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);
			var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Customer/Cart/Summary");

			var response = await PostSummaryHtmlToResponseAsync(client, token, "Mendoza", "Godoy Cruz", SD.DeliveryTypeVendorPickup, pickupCode: null);

			Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
			using var scope = factory.Services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var order = Assert.Single(db.OrderHeaders.AsNoTracking());
			Assert.Equal(SD.DeliveryTypeVendorPickup, order.DeliveryType);
			Assert.Equal(0m, order.ShippingCharge);
			Assert.Equal(100m, order.OrderTotal);
			Assert.Null(order.PickupAgencyCode);
			Assert.Equal(SD.VendorPickupDeliveryNote, order.DeliveryNote);
		}

		[Fact]
		public async Task SummaryPost_MaipuMotorcycle_PersistsChargeAndNoBranch()
		{
			using var factory = new CustomWebApplicationFactory();
			SeedPostalAgencies(factory);
			SeedCart(factory, factory.CustomerEmail, freeShipping: false);
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
			await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);
			var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Customer/Cart/Summary");

			var response = await PostSummaryHtmlToResponseAsync(client, token, "Mendoza", "Godoy Cruz", SD.DeliveryTypeMotorcycle, pickupCode: null);

			Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
			using var scope = factory.Services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var order = Assert.Single(db.OrderHeaders.AsNoTracking());
			Assert.Equal(SD.DeliveryTypeMotorcycle, order.DeliveryType);
			Assert.Equal(100m, order.ShippingCharge);
			Assert.Equal(200m, order.OrderTotal);
			Assert.Null(order.PickupAgencyCode);
			Assert.Null(order.DeliveryNote);
		}

		[Fact]
		public async Task SummaryPost_Company_KeepsBranchAndNoModalityOptions()
		{
			using var factory = new CustomWebApplicationFactory();
			SeedPostalAgencies(factory);
			SeedCart(factory, factory.CompanyEmail, freeShipping: false);
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
			await TestAuthHelper.LoginAsync(client, factory.CompanyEmail, factory.TestPassword);

			var html = await client.GetStringAsync("/en-US/Customer/Cart/Summary");
			Assert.DoesNotContain("Delivery method", html);
			Assert.DoesNotContain("type=\"radio\" name=\"OrderHeader.DeliveryType\"", html);

			var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Customer/Cart/Summary");
			var response = await PostSummaryHtmlToResponseAsync(client, token, "Mendoza", "Godoy Cruz", deliveryType: null, pickupCode: "TEST01");

			Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
			using var scope = factory.Services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var order = Assert.Single(db.OrderHeaders.AsNoTracking());
			Assert.Equal(SD.DeliveryTypePickup, order.DeliveryType);
			Assert.Equal(0m, order.ShippingCharge);
			Assert.Equal("TEST01", order.PickupAgencyCode);
		}

		[Fact]
		public async Task SummaryPost_BranchFromAnotherLocality_RejectedWithoutOrder()
		{
			using var factory = new CustomWebApplicationFactory();
			SeedPostalAgencies(factory);
			SeedCart(factory, factory.CustomerEmail, freeShipping: false);
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
			await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);
			var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Customer/Cart/Summary");

			// Godoy Cruz (province M) selected but a La Plata (province B) branch code posted.
			var response = await PostSummaryHtmlToResponseAsync(client, token, "Mendoza", "Godoy Cruz", SD.DeliveryTypePickup, pickupCode: "TEST03");

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			using var scope = factory.Services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			Assert.Empty(db.OrderHeaders.AsNoTracking());
		}

		[Fact]
		public async Task SummaryPost_UnsupportedModality_RejectedWithoutOrder()
		{
			using var factory = new CustomWebApplicationFactory();
			SeedPostalAgencies(factory);
			SeedCart(factory, factory.CustomerEmail, freeShipping: false);
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
			await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);
			var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/en-US/Customer/Cart/Summary");

			// Motorcycle is only offered in Gran Mendoza; La Plata (province B) must reject it.
			var response = await PostSummaryHtmlToResponseAsync(client, token, "Buenos Aires", "La Plata", SD.DeliveryTypeMotorcycle, pickupCode: null);

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			using var scope = factory.Services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			Assert.Empty(db.OrderHeaders.AsNoTracking());
		}

		private static async Task<HttpResponseMessage> PostSummaryHtmlToResponseAsync(HttpClient client, string token, string state, string locality, string? deliveryType, string? pickupCode)
		{
			var form = new Dictionary<string, string>
			{
				["__RequestVerificationToken"] = token,
				["OrderHeader.Name"] = "Test Customer",
				["OrderHeader.PhoneNumber"] = "555-0100",
				["OrderHeader.StreetAddress"] = "123 Test St",
				["OrderHeader.City"] = locality,
				["OrderHeader.State"] = state,
				["OrderHeader.PostalCode"] = "1000",
				["OrderHeader.PaymentMethod"] = SD.PaymentMethodBankTransfer,
			};
			if (pickupCode is not null) form["OrderHeader.PickupAgencyCode"] = pickupCode;
			if (deliveryType is not null) form["OrderHeader.DeliveryType"] = deliveryType;
			return await client.PostAsync("/en-US/Customer/Cart/Summary", new FormUrlEncodedContent(form));
		}
	}
}
