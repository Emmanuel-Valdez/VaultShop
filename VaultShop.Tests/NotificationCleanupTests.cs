using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VaultShop.DataAccess.Data;
using VaultShop.Models;
using VaultShop.Utility;
using VaultShop.Web.Services.Account;
using VaultShop.Web.Services.Notifications;

namespace VaultShop.Web.Tests;

public class NotificationCleanupTests
{
	[Fact]
	public async Task Cleanup_DeletesExpiredUnreadAndRead_KeepsRecentAndOrders()
	{
		using var factory = new CustomWebApplicationFactory();
		var seed = Seed(factory,
			(DateTime.UtcNow.AddMonths(-7), null),
			(DateTime.UtcNow.AddMonths(-6).AddDays(-1), DateTime.UtcNow.AddMonths(-6)),
			(DateTime.UtcNow.AddMonths(-5), null));

		var deleted = await RunCleanupAsync(factory);

		Assert.Equal(2, deleted);
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var remaining = await db.UserNotifications.AsNoTracking().Select(notification => notification.Id).ToListAsync();
		Assert.Equal(new[] { seed.NotificationIds[2] }, remaining);
		Assert.Equal(3, await db.OrderHeaders.CountAsync(order => seed.OrderIds.Contains(order.Id)));
	}

	[Fact]
	public async Task Cleanup_DeletesExpiredNotificationsAcrossMultipleBatches()
	{
		using var factory = new CustomWebApplicationFactory();
		var count = NotificationCleanupBackgroundService.BatchSize + 4;
		var seed = Seed(factory, Enumerable.Range(0, count)
			.Select(_ => (CreatedAt: DateTime.UtcNow.AddMonths(-7), ReadAt: (DateTime?)null))
			.ToArray());

		var deleted = await RunCleanupAsync(factory);

		Assert.Equal(count, deleted);
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		Assert.False(await db.UserNotifications.AnyAsync(notification => seed.NotificationIds.Contains(notification.Id)));
	}

	[Fact]
	public async Task DeletingPersonalData_RemovesOwnNotifications_AndKeepsOwnOrders()
	{
		using var factory = new CustomWebApplicationFactory();
		string userId;
		int orderId;
		using (var scope = factory.Services.CreateScope())
		{
			var services = scope.ServiceProvider;
			var db = services.GetRequiredService<ApplicationDbContext>();
			var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
			var user = new ApplicationUser
			{
				UserName = "notification.cascade@vaultshop.local",
				Email = "notification.cascade@vaultshop.local",
				Name = "Notification Cascade",
				EmailConfirmed = true
			};
			Assert.True((await userManager.CreateAsync(user, "Test123!")).Succeeded);
			userId = user.Id;

			var order = NewOrder(user.Id, "Own Cascade Order");
			db.OrderHeaders.Add(order);
			db.SaveChanges();
			orderId = order.Id;

			db.UserNotifications.Add(new UserNotification
			{
				ApplicationUserId = user.Id,
				OrderId = order.Id,
				Type = OrderNotificationType.Shipped
			});
			db.SaveChanges();

			var deletion = services.GetRequiredService<AccountDeletionService>();
			Assert.True(await deletion.DeletePersonalDataAsync(user.Id));
		}

		using var verifyScope = factory.Services.CreateScope();
		var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		Assert.Equal(0, await verifyDb.UserNotifications.CountAsync());
		var keptOrder = await verifyDb.OrderHeaders.SingleAsync(order => order.Id == orderId);
		Assert.Equal(userId, keptOrder.ApplicationUserId);
		Assert.Equal("Own Cascade Order", keptOrder.Name);
		Assert.Equal("123 Test St", keptOrder.StreetAddress);
	}

	private static async Task<int> RunCleanupAsync(CustomWebApplicationFactory factory)
	{
		var service = new NotificationCleanupBackgroundService(
			factory.Services,
			NullLogger<NotificationCleanupBackgroundService>.Instance);
		using var scope = factory.Services.CreateScope();
		return await service.CleanupScopeAsync(scope.ServiceProvider, CancellationToken.None);
	}

	private static CleanupSeed Seed(
		CustomWebApplicationFactory factory,
		params (DateTime CreatedAt, DateTime? ReadAt)[] notifications)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var customer = db.ApplicationUsers.Single(user => user.Email == factory.CustomerEmail);
		var orders = Enumerable.Range(0, notifications.Length)
			.Select(index => NewOrder(customer.Id, $"Cleanup Order {index}"))
			.ToList();
		db.OrderHeaders.AddRange(orders);
		db.SaveChanges();

		var rows = orders.Select((order, index) => new UserNotification
		{
			ApplicationUserId = customer.Id,
			OrderId = order.Id,
			Type = OrderNotificationType.PaymentApproved,
			CreatedAt = notifications[index].CreatedAt,
			ReadAt = notifications[index].ReadAt
		}).ToList();
		db.UserNotifications.AddRange(rows);
		db.SaveChanges();

		return new CleanupSeed(
			rows.Select(notification => notification.Id).ToArray(),
			orders.Select(order => order.Id).ToArray());
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

	private sealed record CleanupSeed(int[] NotificationIds, int[] OrderIds);
}
