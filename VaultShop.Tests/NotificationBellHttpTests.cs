using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VaultShop.DataAccess.Data;
using VaultShop.Models;
using VaultShop.Utility;

namespace VaultShop.Web.Tests;

public class NotificationBellHttpTests
{
	[Fact]
	public async Task CustomerBell_ShowsOnlyTheirUnreadCountWithAccessibleLabel()
	{
		using var factory = new CustomWebApplicationFactory();
		SeedNotifications(factory);
		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

		var body = await client.GetStringAsync("/en-US/Customer/Home/Index");

		Assert.Contains("data-notification-count=\"2\"", body);
		Assert.Contains("aria-label=\"Notifications: 2 unread\"", body);
		Assert.Contains("href=\"/Identity/Account/Notifications\"", NotificationBell(body));
	}

	[Fact]
	public async Task CustomerBell_WithNoUnreadNotifications_HasNoCountBadge()
	{
		using var factory = new CustomWebApplicationFactory();
		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

		var body = await client.GetStringAsync("/en-US/Customer/Home/Index");
		var bell = NotificationBell(body);

		Assert.Contains("aria-label=\"Notifications\"", bell);
		Assert.DoesNotContain("data-notification-count", bell);
	}

	[Theory]
	[InlineData("customer")]
	[InlineData("company")]
	[InlineData("admin")]
	public async Task EligibleUser_SeesNotificationBell(string role)
	{
		using var factory = new CustomWebApplicationFactory();
		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		var email = role switch
		{
			"company" => factory.CompanyEmail,
			"admin" => factory.AdminEmail,
			_ => factory.CustomerEmail
		};
		await TestAuthHelper.LoginAsync(client, email, factory.TestPassword);

		var body = await client.GetStringAsync("/en-US/Customer/Home/Index");

		Assert.Contains("data-notification-bell=\"true\"", body);
	}

	[Theory]
	[InlineData("anonymous")]
	[InlineData("employee")]
	public async Task AnonymousOrEmployee_DoesNotSeeNotificationBell(string userType)
	{
		using var factory = new CustomWebApplicationFactory();
		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		if (userType == "employee")
		{
			await TestAuthHelper.LoginAsync(client, factory.EmployeeEmail, factory.TestPassword);
		}

		var body = await client.GetStringAsync("/en-US/Customer/Home/Index");

		Assert.DoesNotContain("data-notification-bell", body);
	}

	private static string NotificationBell(string body)
	{
		var match = Regex.Match(body, "(?s)<a\\b(?=[^>]*data-notification-bell=\\\"true\\\")[^>]*>.*?</a>");
		Assert.True(match.Success, "Expected the notification bell in the shared header.");
		return match.Value;
	}

	private static void SeedNotifications(CustomWebApplicationFactory factory)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var customer = db.ApplicationUsers.Single(user => user.Email == factory.CustomerEmail);
		var admin = db.ApplicationUsers.Single(user => user.Email == factory.AdminEmail);
		var order = new OrderHeader
		{
			ApplicationUserId = customer.Id,
			OrderDate = DateTime.UtcNow,
			ShippingDate = DateTime.UtcNow,
			PaymentDate = DateTime.UtcNow,
			PaymentDueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
			OrderTotal = 100m,
			PaymentMethod = SD.PaymentMethodBankTransfer,
			PaymentStatus = SD.PaymentStatusPending,
			OrderStatus = SD.StatusPending,
			Name = "Test Customer",
			StreetAddress = "123 Test St",
			City = "Buenos Aires",
			State = "Buenos Aires",
			PostalCode = "1000",
			PhoneNumber = "555-0100"
		};
		db.OrderHeaders.Add(order);
		db.SaveChanges();

		db.UserNotifications.AddRange(
			new UserNotification { ApplicationUserId = customer.Id, OrderId = order.Id, Type = OrderNotificationType.PaymentApproved },
			new UserNotification { ApplicationUserId = customer.Id, OrderId = order.Id, Type = OrderNotificationType.Shipped },
			new UserNotification { ApplicationUserId = customer.Id, OrderId = order.Id, Type = OrderNotificationType.PaymentFailed, ReadAt = DateTime.UtcNow },
			new UserNotification { ApplicationUserId = admin.Id, OrderId = order.Id, Type = OrderNotificationType.AdminNewOrder });
		db.SaveChanges();
	}
}
