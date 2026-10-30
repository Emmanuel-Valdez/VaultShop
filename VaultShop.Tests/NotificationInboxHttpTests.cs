using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VaultShop.DataAccess.Data;
using VaultShop.Models;
using VaultShop.Utility;

namespace VaultShop.Web.Tests;

public class NotificationInboxHttpTests
{
	[Fact]
	public async Task Inbox_ShowsOnlyTheCurrentUsersNotifications()
	{
		using var factory = new CustomWebApplicationFactory();
		var (customerOrderId, adminOrderId) = SeedCrossUserNotifications(factory);
		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);
		await SetCultureAsync(client, "en-US");

		var body = await client.GetStringAsync("/Identity/Account/Notifications");

		Assert.Contains("Payment approved", body);
		Assert.Contains($"order #{customerOrderId}", body);
		Assert.DoesNotContain("New order", body);
		Assert.DoesNotContain($"order #{adminOrderId}", body);
	}

	[Fact]
	public async Task Inbox_OrdersNotificationsNewestFirst()
	{
		using var factory = new CustomWebApplicationFactory();
		SeedNotifications(factory, (OrderNotificationType.PaymentApproved, DateTime.UtcNow.AddHours(-2)),
			(OrderNotificationType.Shipped, DateTime.UtcNow.AddHours(-1)));
		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);
		await SetCultureAsync(client, "en-US");

		var body = await client.GetStringAsync("/Identity/Account/Notifications");

		var newer = body.IndexOf("Order shipped", StringComparison.Ordinal);
		var older = body.IndexOf("Payment approved", StringComparison.Ordinal);
		Assert.True(newer >= 0 && older >= 0, "Expected both seeded notifications in the inbox.");
		Assert.True(newer < older, "Expected the newest notification first.");
	}

	[Fact]
	public async Task OpeningInbox_MarksEveryUnreadNotificationAsRead()
	{
		using var factory = new CustomWebApplicationFactory();
		var orderId = SeedNotifications(factory,
			(OrderNotificationType.PaymentApproved, DateTime.UtcNow.AddHours(-2)),
			(OrderNotificationType.Shipped, DateTime.UtcNow.AddHours(-1)));
		var previouslyReadAt = DateTime.UtcNow.AddDays(-1);
		using (var scope = factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var customer = db.ApplicationUsers.Single(user => user.Email == factory.CustomerEmail);
			db.UserNotifications.Add(new UserNotification
			{
				ApplicationUserId = customer.Id,
				OrderId = orderId,
				Type = OrderNotificationType.PaymentFailed,
				CreatedAt = DateTime.UtcNow.AddHours(-3),
				ReadAt = previouslyReadAt
			});
			db.SaveChanges();
		}

		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);
		await SetCultureAsync(client, "en-US");

		var body = await client.GetStringAsync("/Identity/Account/Notifications");
		Assert.Contains("Unread", body);

		using var verifyScope = factory.Services.CreateScope();
		var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var customerId = verifyDb.ApplicationUsers.Single(user => user.Email == factory.CustomerEmail).Id;
		var notifications = await verifyDb.UserNotifications
			.AsNoTracking()
			.Where(notification => notification.ApplicationUserId == customerId)
			.ToListAsync();

		Assert.Equal(3, notifications.Count);
		Assert.All(notifications, notification => Assert.NotNull(notification.ReadAt));
		Assert.Equal(previouslyReadAt, notifications.Single(notification => notification.Type == OrderNotificationType.PaymentFailed).ReadAt);
	}

	[Fact]
	public async Task Inbox_LanguageSwitch_RendersInTheNewCulture()
	{
		using var factory = new CustomWebApplicationFactory();
		SeedNotifications(factory, (OrderNotificationType.PaymentApproved, DateTime.UtcNow));
		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

		await SetCultureAsync(client, "en-US");
		var english = await client.GetStringAsync("/Identity/Account/Notifications");
		Assert.Contains("Payment approved", english);

		await SetCultureAsync(client, "es-AR");
		var spanish = await client.GetStringAsync("/Identity/Account/Notifications");
		Assert.Contains("Pago aprobado", spanish);
		Assert.DoesNotContain("Payment approved", spanish);
	}

	[Fact]
	public async Task Inbox_WithNoNotifications_ShowsEmptyState()
	{
		using var factory = new CustomWebApplicationFactory();
		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);
		await SetCultureAsync(client, "en-US");

		var body = await client.GetStringAsync("/Identity/Account/Notifications");

		Assert.Contains("You have no notifications.", body);
		Assert.DoesNotContain("data-notifications-list", body);
	}

	[Fact]
	public async Task Inbox_LinksToTheExistingOrderDetailsPage()
	{
		using var factory = new CustomWebApplicationFactory();
		var orderId = SeedNotifications(factory, (OrderNotificationType.PaymentApproved, DateTime.UtcNow));
		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);
		await SetCultureAsync(client, "en-US");

		var body = await client.GetStringAsync("/Identity/Account/Notifications");

		Assert.Contains($"/en-us/admin/order/details?orderid={orderId}", body.ToLowerInvariant());
	}

	private static async Task SetCultureAsync(HttpClient client, string culture)
	{
		var response = await client.GetAsync(
			$"/{culture}/Customer/Home/SetLanguage?culture={culture}&returnUrl=%2FCustomer%2FHome%2FIndex");
		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
	}

	private static (int CustomerOrderId, int AdminOrderId) SeedCrossUserNotifications(CustomWebApplicationFactory factory)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var customer = db.ApplicationUsers.Single(user => user.Email == factory.CustomerEmail);
		var admin = db.ApplicationUsers.Single(user => user.Email == factory.AdminEmail);
		var customerOrder = NewOrder(customer.Id, "Customer Order");
		var adminOrder = NewOrder(admin.Id, "Admin Order");
		db.OrderHeaders.AddRange(customerOrder, adminOrder);
		db.SaveChanges();

		db.UserNotifications.AddRange(
			new UserNotification
			{
				ApplicationUserId = customer.Id,
				OrderId = customerOrder.Id,
				Type = OrderNotificationType.PaymentApproved
			},
			new UserNotification
			{
				ApplicationUserId = admin.Id,
				OrderId = adminOrder.Id,
				Type = OrderNotificationType.AdminNewOrder
			});
		db.SaveChanges();

		return (customerOrder.Id, adminOrder.Id);
	}

	private static int SeedNotifications(
		CustomWebApplicationFactory factory,
		params (OrderNotificationType Type, DateTime CreatedAt)[] notifications)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var customer = db.ApplicationUsers.Single(user => user.Email == factory.CustomerEmail);
		var order = NewOrder(customer.Id, "Notifications Order");
		db.OrderHeaders.Add(order);
		db.SaveChanges();

		foreach (var notification in notifications)
		{
			db.UserNotifications.Add(new UserNotification
			{
				ApplicationUserId = customer.Id,
				OrderId = order.Id,
				Type = notification.Type,
				CreatedAt = notification.CreatedAt
			});
		}
		db.SaveChanges();

		return order.Id;
	}

	private static OrderHeader NewOrder(string applicationUserId, string name) => new()
	{
		ApplicationUserId = applicationUserId,
		OrderDate = DateTime.UtcNow,
		ShippingDate = DateTime.UtcNow,
		PaymentDate = DateTime.UtcNow,
		PaymentDueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
		OrderTotal = 100m,
		PaymentMethod = SD.PaymentMethodBankTransfer,
		PaymentStatus = SD.PaymentStatusPending,
		OrderStatus = SD.StatusPending,
		Name = name,
		StreetAddress = "123 Test St",
		City = "Buenos Aires",
		State = "Buenos Aires",
		PostalCode = "1000",
		PhoneNumber = "555-0100"
	};
}
