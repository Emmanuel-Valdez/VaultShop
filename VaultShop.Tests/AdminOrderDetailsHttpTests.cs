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

		private static int SeedShippedPickupOrder(CustomWebApplicationFactory factory)
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
