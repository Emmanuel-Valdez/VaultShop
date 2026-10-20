using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using VaultShop.DataAccess.Data;
using VaultShop.Models;
using VaultShop.Utility;

namespace VaultShop.Web.Tests
{
	public class AdminOrderDetailsHttpTests
	{
		private static void SeedBranches(CustomWebApplicationFactory factory)
		{
			using var scope = factory.Services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			db.PostalAgencies.AddRange(
				new PostalAgency
				{
					Code = "OLD01",
					Name = "Sucursal Vieja",
					Street = "Vieja",
					Number = 1,
					Locality = "Capital",
					City = "Capital",
					Province = "Mendoza",
					ProvinceCode = "M",
					PostalCode = "M5500",
					Source = "correo",
					Services = "40",
					Hours = "LUN A VIE 9 A 18",
				},
				new PostalAgency
				{
					Code = "NEW01",
					Name = "Sucursal Nueva",
					Street = "Nueva",
					Number = 9,
					Locality = "Capital",
					City = "Capital",
					Province = "Mendoza",
					ProvinceCode = "M",
					PostalCode = "M5500",
					Source = "correo",
					Services = "40",
					Hours = "LUN A SAB 8 A 17",
				});
			db.SaveChanges();
		}

		private static int SeedShippedPickupOrder(CustomWebApplicationFactory factory, string? paymentDiscountMotive = null)
		{
			using var scope = factory.Services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var customer = db.ApplicationUsers.Single(u => u.Email == factory.CustomerEmail);
			var order = new OrderHeader
			{
				ApplicationUserId = customer.Id,
				OrderDate = DateTime.UtcNow,
				OrderTotal = 100m,
				OrderStatus = SD.StatusShipped,
				PaymentStatus = SD.PaymentStatusApproved,
				PaymentMethod = SD.PaymentMethodBankTransfer,
				Name = "Test Customer",
				StreetAddress = "123 Test St",
				City = "Buenos Aires",
				State = "Buenos Aires",
				PostalCode = "1000",
				PhoneNumber = "555-0100",
				DeliveryType = SD.DeliveryTypePickup,
				PickupAgencyCode = "OLD01",
				PickupAgencyName = "Sucursal Vieja",
				PickupAgencyAddress = "Vieja 1, Capital, Mendoza M5500",
				PickupAgencyHours = "LUN A VIE 9 A 18",
				PaymentDiscountTotal = 10m,
				PaymentDiscountMotive = paymentDiscountMotive,
			};
			db.OrderHeaders.Add(order);
			db.SaveChanges();
			return order.Id;
		}

		[Fact]
		public async Task Details_ShippedPickupOrder_RendersReadOnly_WithNoMutatingActions()
		{
			using var factory = new CustomWebApplicationFactory();
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

			var orderId = SeedShippedPickupOrder(factory);
			await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);

			var response = await client.GetAsync($"/en-US/Admin/Order/Details?orderId={orderId}");
			var html = await response.Content.ReadAsStringAsync();

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			// Snapshot still visible, including hours.
			Assert.Contains("OLD01", html);
			Assert.Contains("LUN A VIE 9 A 18", html);
			// No mutating actions or editable inputs.
			Assert.DoesNotContain("UpdateOrderDetail\"", html);
			Assert.DoesNotContain("StartProcessing\"", html);
			Assert.DoesNotContain("ShipOrder\"", html);
			Assert.DoesNotContain("CancelOrder\"", html);
			Assert.DoesNotContain("<button type=\"submit\"", html);
			Assert.DoesNotContain("confirmShippedOrderUpdate", html);
			Assert.DoesNotContain("adminBranchPicker", html);
			Assert.DoesNotContain("name=\"OrderHeader.Carrier\" id=\"carrier\"", html);
		}
		[Theory]
		[InlineData("en-US", "Hours")]
		[InlineData("es-AR", "Horario")]
		public async Task Summary_PickupOrder_RendersHoursLine(string culture, string hoursLabel)
		{
			using var factory = new CustomWebApplicationFactory();
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

			var orderId = SeedShippedPickupOrder(factory);
			await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);

			var response = await client.GetAsync($"/{culture}/Admin/Order/Summary?orderId={orderId}");
			var html = await response.Content.ReadAsStringAsync();

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			Assert.Contains("Sucursal Vieja", html);
			Assert.Contains($"{hoursLabel}: LUN A VIE 9 A 18", html);
		}

		[Theory]
		[InlineData("en-US", "Details")]
		[InlineData("es-AR", "Summary")]
		public async Task OrderSurfaces_RenderFrozenPaymentDiscountMotive(string culture, string page)
		{
			using var factory = new CustomWebApplicationFactory();
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

			var orderId = SeedShippedPickupOrder(factory, paymentDiscountMotive: "Transferencia -10%");
			await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);

			var response = await client.GetAsync($"/{culture}/Admin/Order/{page}?orderId={orderId}");
			var html = await response.Content.ReadAsStringAsync();

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			Assert.Contains("Transferencia -10%", html);
		}

		[Fact]
		public async Task UpdateOrderDetail_CorrectsBranch_ThroughRealForm_AsAdmin()
		{
			using var factory = new CustomWebApplicationFactory();
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

			SeedBranches(factory);
			var orderId = SeedUnshippedPickupOrder(factory);
			await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);

			var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, $"/en-US/Admin/Order/Details?orderId={orderId}");
			// The readonly snapshot input must not post a duplicate PickupAgencyCode that shadows the picker radios.
			var response = await client.PostAsync("/en-US/Admin/Order/UpdateOrderDetail", new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["OrderHeader.Id"] = orderId.ToString(),
				["OrderHeader.Name"] = "Test Customer",
				["OrderHeader.PhoneNumber"] = "555-0100",
				["OrderHeader.StreetAddress"] = "123 Test St",
				["OrderHeader.City"] = "Buenos Aires",
				["OrderHeader.State"] = "Buenos Aires",
				["OrderHeader.PostalCode"] = "1000",
				["OrderHeader.PickupAgencyCode"] = "NEW01",
				["__RequestVerificationToken"] = token,
			}));

			Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
			Assert.Contains($"/en-US/Admin/Order/Details?orderId={orderId}", response.Headers.Location?.ToString());

			using (var scope = factory.Services.CreateScope())
			{
				var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
				var order = db.OrderHeaders.Single(o => o.Id == orderId);
				Assert.Equal("NEW01", order.PickupAgencyCode);
				Assert.Equal("Sucursal Nueva", order.PickupAgencyName);
				Assert.Contains("Nueva 9", order.PickupAgencyAddress);
				Assert.Contains("M5500", order.PickupAgencyAddress);
				Assert.Equal("LUN A SAB 8 A 17", order.PickupAgencyHours);
			}
		}

		[Theory]
		[InlineData("en-US", "Motorcycle delivery to your home")]
		[InlineData("es-AR", "moto")]
		public async Task Details_MotorcycleOrder_ShowsFrozenChargeAndModality(string culture, string modalityFragment)
		{
			using var factory = new CustomWebApplicationFactory();
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

			var orderId = SeedOrder(factory, SD.DeliveryTypeMotorcycle, shippingCharge: 250m, deliveryNote: null);
			await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);

			var response = await client.GetAsync($"/{culture}/Admin/Order/Details?orderId={orderId}");
			var html = await response.Content.ReadAsStringAsync();

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			Assert.Contains("250", html);
			Assert.Contains(modalityFragment, html);
		}

		[Fact]
		public async Task Details_PreChangeOrder_RendersFreeShippingAndNoModality()
		{
			using var factory = new CustomWebApplicationFactory();
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

			var orderId = SeedOrder(factory, deliveryType: null, shippingCharge: 0m, deliveryNote: null);
			await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);

			var response = await client.GetAsync($"/en-US/Admin/Order/Details?orderId={orderId}");
			var html = await response.Content.ReadAsStringAsync();

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			Assert.Contains("Free shipping", html);
			Assert.DoesNotContain("Motorcycle delivery to your home", html);
			Assert.DoesNotContain("Pickup to coordinate with the seller", html);
		}

		[Fact]
		public async Task Details_VendorPickupOrder_ShowsRecordedWording()
		{
			using var factory = new CustomWebApplicationFactory();
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

			var orderId = SeedOrder(factory, SD.DeliveryTypeVendorPickup, shippingCharge: 0m, deliveryNote: SD.VendorPickupDeliveryNote);
			await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);

			var response = await client.GetAsync("/en-US/Admin/Order/Details?orderId=" + orderId);
			var html = await response.Content.ReadAsStringAsync();

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			Assert.Contains("Pickup to coordinate with the seller", html);
			Assert.Contains("Microcentro", html);
		}

		[Fact]
		public async Task Summary_MotorcycleOrder_ShowsChargeAndModality()
		{
			using var factory = new CustomWebApplicationFactory();
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

			var orderId = SeedOrder(factory, SD.DeliveryTypeMotorcycle, shippingCharge: 250m, deliveryNote: null);
			await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);

			var response = await client.GetAsync($"/en-US/Admin/Order/Summary?orderId={orderId}");
			var html = await response.Content.ReadAsStringAsync();

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			Assert.Contains("250", html);
			Assert.Contains("Motorcycle delivery to your home", html);
		}

		[Fact]
		public async Task OrderConfirmation_ChargedOrder_ShowsShippingAndModality()
		{
			using var factory = new CustomWebApplicationFactory();
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

			var orderId = SeedOrder(factory, SD.DeliveryTypeMotorcycle, shippingCharge: 250m, deliveryNote: null, sessionId: "sess-1");
			await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

			var response = await client.GetAsync($"/en-US/Customer/Cart/OrderConfirmation?id={orderId}&session_id=sess-1");
			var html = await response.Content.ReadAsStringAsync();

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			Assert.Contains("Shipping", html);
			Assert.Contains("250", html);
			Assert.Contains("Motorcycle delivery to your home", html);
		}

		[Fact]
		public async Task OrderConfirmation_PreChangeOrder_RendersWithoutShippingBlock()
		{
			using var factory = new CustomWebApplicationFactory();
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

			var orderId = SeedOrder(factory, deliveryType: null, shippingCharge: 0m, deliveryNote: null, sessionId: "sess-2");
			await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

			var response = await client.GetAsync($"/en-US/Customer/Cart/OrderConfirmation?id={orderId}&session_id=sess-2");
			var html = await response.Content.ReadAsStringAsync();

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			Assert.DoesNotContain("Delivery method", html);
			Assert.DoesNotContain("Free shipping", html);
		}

		private static int SeedOrder(CustomWebApplicationFactory factory, string? deliveryType, decimal shippingCharge, string? deliveryNote, string sessionId = "", int companyId = 0)
		{
			using var scope = factory.Services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var customer = db.ApplicationUsers.Single(u => u.Email == factory.CustomerEmail);
			var order = new OrderHeader
			{
				ApplicationUserId = customer.Id,
				CompanyId = companyId > 0 ? companyId : null,
				OrderDate = DateTime.UtcNow,
				OrderTotal = 100m + shippingCharge,
				ShippingCharge = shippingCharge,
				OrderStatus = SD.StatusShipped,
				PaymentStatus = SD.PaymentStatusApproved,
				PaymentMethod = SD.PaymentMethodBankTransfer,
				Name = "Test Customer",
				StreetAddress = "123 Test St",
				City = "Godoy Cruz",
				State = "Mendoza",
				PostalCode = "5501",
				PhoneNumber = "555-0100",
				DeliveryType = deliveryType,
				DeliveryNote = deliveryNote,
				SessionId = sessionId,
			};
			db.OrderHeaders.Add(order);
			db.SaveChanges();
			return order.Id;
		}

		[Fact]
		public async Task Details_WholesaleOrder_OmitsShippingRow()
		{
			using var factory = new CustomWebApplicationFactory();
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

			var orderId = SeedOrder(factory, SD.DeliveryTypePickup, shippingCharge: 0m, deliveryNote: null, companyId: factory.TestCompanyId);
			await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);

			var response = await client.GetAsync($"/en-US/Admin/Order/Details?orderId={orderId}");
			var html = await response.Content.ReadAsStringAsync();

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			Assert.DoesNotContain("Free shipping", html);
			Assert.DoesNotContain("Envío gratis", html);
		}

		[Fact]
		public async Task Summary_WholesaleOrder_OmitsShippingRow()
		{
			using var factory = new CustomWebApplicationFactory();
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

			var orderId = SeedOrder(factory, SD.DeliveryTypePickup, shippingCharge: 0m, deliveryNote: null, companyId: factory.TestCompanyId);
			await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);

			var response = await client.GetAsync($"/en-US/Admin/Order/Summary?orderId={orderId}");
			var html = await response.Content.ReadAsStringAsync();

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			Assert.DoesNotContain("Free shipping", html);
		}

		private static int SeedUnshippedPickupOrder(CustomWebApplicationFactory factory)
		{
			using var scope = factory.Services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var customer = db.ApplicationUsers.Single(u => u.Email == factory.CustomerEmail);
			var order = new OrderHeader
			{
				ApplicationUserId = customer.Id,
				OrderDate = DateTime.UtcNow,
				OrderTotal = 100m,
				OrderStatus = SD.StatusApproved,
				PaymentStatus = SD.PaymentStatusApproved,
				PaymentMethod = SD.PaymentMethodBankTransfer,
				Name = "Test Customer",
				StreetAddress = "123 Test St",
				City = "Buenos Aires",
				State = "Buenos Aires",
				PostalCode = "1000",
				PhoneNumber = "555-0100",
				DeliveryType = SD.DeliveryTypePickup,
				PickupAgencyCode = "OLD01",
				PickupAgencyName = "Sucursal Vieja",
				PickupAgencyAddress = "Vieja 1, Capital, Mendoza M5500",
				PickupAgencyHours = "LUN A VIE 9 A 18",
			};
			db.OrderHeaders.Add(order);
			db.SaveChanges();
			return order.Id;
		}
	}
}
