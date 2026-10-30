using System.Net;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using VaultShop.DataAccess.Data;
using VaultShop.Models;
using VaultShop.Utility;
using VaultShop.Web.Services.Payments;

namespace VaultShop.Web.Tests;

public class OrderNotificationHooksHttpTests
{
	[Fact]
	public async Task ShipOrderRequest_PersistsShippedNotification()
	{
		using var factory = new CustomWebApplicationFactory();
		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		var orderId = SeedOrder(factory, factory.CustomerEmail, SD.PaymentMethodBankTransfer, SD.PaymentStatusApproved, SD.StatusInProcess);
		await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);
		var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, $"/en-US/Admin/Order/Details?orderId={orderId}");
		var form = new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token,
			["OrderHeader.Id"] = orderId.ToString(),
			["OrderHeader.Carrier"] = "Test Carrier",
			["OrderHeader.TrackingNumber"] = "TRACK-HTTP-1"
		};

		var response = await client.PostAsync("/en-US/Admin/Order/ShipOrder", new FormUrlEncodedContent(form));

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var notification = Assert.Single(await db.UserNotifications.AsNoTracking().ToListAsync());
		var order = await db.OrderHeaders.AsNoTracking().SingleAsync(item => item.Id == orderId);
		Assert.Equal(orderId, notification.OrderId);
		Assert.Equal(order.ApplicationUserId, notification.ApplicationUserId);
		Assert.Equal(OrderNotificationType.Shipped, notification.Type);
	}

	[Fact]
	public async Task ShippingEmailFailure_DoesNotSuppressPersistedNotification()
	{
		var emailSender = new Mock<IEmailSender>();
		emailSender.Setup(sender => sender.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
			.ThrowsAsync(new InvalidOperationException("email unavailable"));
		using var baseFactory = new CustomWebApplicationFactory();
		using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
		{
			services.RemoveAll<IEmailSender>();
			services.AddSingleton(emailSender.Object);
		}));
		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		var orderId = SeedOrder(factory, baseFactory.CustomerEmail, SD.PaymentMethodBankTransfer, SD.PaymentStatusApproved, SD.StatusInProcess);
		await TestAuthHelper.LoginAsync(client, baseFactory.AdminEmail, baseFactory.TestPassword);
		var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, $"/en-US/Admin/Order/Details?orderId={orderId}");
		var form = new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token,
			["OrderHeader.Id"] = orderId.ToString(),
			["OrderHeader.Carrier"] = "Test Carrier",
			["OrderHeader.TrackingNumber"] = "TRACK-EMAIL-FAIL"
		};

		var response = await client.PostAsync("/en-US/Admin/Order/ShipOrder", new FormUrlEncodedContent(form));

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		emailSender.Verify(sender => sender.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var notification = Assert.Single(await db.UserNotifications.AsNoTracking().ToListAsync());
		Assert.Equal(orderId, notification.OrderId);
		Assert.Equal(OrderNotificationType.Shipped, notification.Type);
	}

	[Fact]
	public async Task CancelOrderRequestWithoutRefund_PersistsCancelledNotification()
	{
		using var factory = new CustomWebApplicationFactory();
		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		var orderId = SeedOrder(factory, factory.CustomerEmail, SD.PaymentMethodBankTransfer, SD.PaymentStatusApproved, SD.StatusApproved);
		await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);
		var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, $"/en-US/Admin/Order/Details?orderId={orderId}");
		var form = new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token,
			["OrderHeader.Id"] = orderId.ToString()
		};

		var response = await client.PostAsync("/en-US/Admin/Order/CancelOrder", new FormUrlEncodedContent(form));

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var notification = Assert.Single(await db.UserNotifications.AsNoTracking().ToListAsync());
		Assert.Equal(orderId, notification.OrderId);
		Assert.Equal(OrderNotificationType.Cancelled, notification.Type);
	}

	[Fact]
	public async Task ConfirmTransferSentWithNoAdminEmail_PersistsOneAdminNotification()
	{
		using var baseFactory = new CustomWebApplicationFactory();
		using var factory = baseFactory.WithWebHostBuilder(builder =>
			builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Email:AdminEmail"] = string.Empty,
			})));
		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		var orderId = SeedOrder(factory, baseFactory.CustomerEmail, SD.PaymentMethodBankTransfer, SD.PaymentStatusPending, SD.StatusPending);
		await TestAuthHelper.LoginAsync(client, baseFactory.CustomerEmail, baseFactory.TestPassword);
		var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, $"/en-US/Admin/Order/Details?orderId={orderId}");
		var firstResponse = await client.PostAsync($"/en-US/Admin/Order/ConfirmTransferSent?orderId={orderId}",
			new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
		var secondResponse = await client.PostAsync($"/en-US/Admin/Order/ConfirmTransferSent?orderId={orderId}",
			new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

		Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);
		Assert.Equal(HttpStatusCode.Redirect, secondResponse.StatusCode);
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var notification = Assert.Single(await db.UserNotifications.AsNoTracking().ToListAsync());
		var adminId = db.ApplicationUsers.Single(user => user.Email == baseFactory.AdminEmail).Id;
		Assert.Equal(adminId, notification.ApplicationUserId);
		Assert.Equal(orderId, notification.OrderId);
		Assert.Equal(OrderNotificationType.AdminBankTransferRequest, notification.Type);
	}

	private static int SeedOrder(
		WebApplicationFactory<Program> factory,
		string userEmail,
		string paymentMethod,
		string paymentStatus,
		string orderStatus)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var user = db.ApplicationUsers.Single(item => item.Email == userEmail);
		var order = new OrderHeader
		{
			ApplicationUserId = user.Id,
			OrderDate = DateTime.UtcNow,
			ShippingDate = DateTime.UtcNow,
			PaymentDate = DateTime.UtcNow,
			PaymentDueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
			OrderTotal = 100m,
			PaymentMethod = paymentMethod,
			PaymentStatus = paymentStatus,
			OrderStatus = orderStatus,
			PaymentIntentId = paymentMethod == SD.PaymentMethodStripe ? "pi_test_refund" : null,
			Name = user.Name,
			StreetAddress = "123 Test St",
			City = "Buenos Aires",
			State = "Buenos Aires",
			PostalCode = "1000",
			PhoneNumber = "555-0100"
		};
		db.OrderHeaders.Add(order);
		db.SaveChanges();
		return order.Id;
	}
}
