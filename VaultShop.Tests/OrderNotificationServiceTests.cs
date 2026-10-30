using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Utility;
using VaultShop.Web.Services.Notifications;

namespace VaultShop.Web.Tests;

public class OrderNotificationServiceTests
{
	[Fact]
	public async Task NotifyOrderEventAsync_CreatesCustomerNotificationFromOrderUserId()
	{
		var unitOfWork = new TestUnitOfWork(new OrderHeader { Id = 42, ApplicationUserId = "customer-1" });
		var userManager = CreateUserManager();
		var logger = new Mock<ILogger<OrderNotificationService>>();
		var service = new OrderNotificationService(unitOfWork.UnitOfWork.Object, userManager.Object, logger.Object);

		await service.NotifyOrderEventAsync(42, OrderNotificationType.PaymentApproved);

		var notification = Assert.Single(unitOfWork.Notifications);
		Assert.Equal("customer-1", notification.ApplicationUserId);
		Assert.Equal(42, notification.OrderId);
		Assert.Equal(OrderNotificationType.PaymentApproved, notification.Type);
		unitOfWork.OrderHeader.Verify(repository => repository.Get(
			It.IsAny<Expression<Func<OrderHeader, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()), Times.Once);
		unitOfWork.UnitOfWork.Verify(work => work.Save(), Times.Once);
		userManager.Verify(manager => manager.GetUsersInRoleAsync(It.IsAny<string>()), Times.Never);
	}

	[Fact]
	public async Task NotifyOrderEventAsync_FansOutToEveryAdminAndDoesNotQueryEmployeeRole()
	{
		var unitOfWork = new TestUnitOfWork(new OrderHeader { Id = 42, ApplicationUserId = "customer-1" });
		var userManager = CreateUserManager(
			new ApplicationUser { Id = "admin-1" },
			new ApplicationUser { Id = "admin-2" });
		var logger = new Mock<ILogger<OrderNotificationService>>();
		var service = new OrderNotificationService(unitOfWork.UnitOfWork.Object, userManager.Object, logger.Object);

		await service.NotifyOrderEventAsync(42, OrderNotificationType.AdminNewOrder);

		Assert.Equal(new[] { "admin-1", "admin-2" }, unitOfWork.Notifications.Select(item => item.ApplicationUserId).Order());
		Assert.All(unitOfWork.Notifications, notification => Assert.Equal(OrderNotificationType.AdminNewOrder, notification.Type));
		userManager.Verify(manager => manager.GetUsersInRoleAsync(SD.Role_Admin), Times.Once);
		userManager.Verify(manager => manager.GetUsersInRoleAsync(SD.Role_Employee), Times.Never);
		unitOfWork.OrderHeader.Verify(repository => repository.Get(
			It.IsAny<Expression<Func<OrderHeader, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()), Times.Once);
	}

	[Fact]
	public async Task NotifyOrderEventAsync_DoesNotCreateCustomerNotificationForGuestOrder()
	{
		var unitOfWork = new TestUnitOfWork(new OrderHeader { Id = 42, ApplicationUserId = string.Empty });
		var userManager = CreateUserManager();
		var logger = new Mock<ILogger<OrderNotificationService>>();
		var service = new OrderNotificationService(unitOfWork.UnitOfWork.Object, userManager.Object, logger.Object);

		await service.NotifyOrderEventAsync(42, OrderNotificationType.PaymentFailed);

		Assert.Empty(unitOfWork.Notifications);
		unitOfWork.UnitOfWork.Verify(work => work.Save(), Times.Never);
		userManager.Verify(manager => manager.GetUsersInRoleAsync(It.IsAny<string>()), Times.Never);
	}

	[Fact]
	public async Task NotifyOrderEventAsync_IsIdempotentForRepeatedEvents()
	{
		var unitOfWork = new TestUnitOfWork(new OrderHeader { Id = 42, ApplicationUserId = "customer-1" });
		var service = new OrderNotificationService(
			unitOfWork.UnitOfWork.Object,
			CreateUserManager().Object,
			new Mock<ILogger<OrderNotificationService>>().Object);

		await service.NotifyOrderEventAsync(42, OrderNotificationType.Shipped);
		await service.NotifyOrderEventAsync(42, OrderNotificationType.Shipped);

		Assert.Single(unitOfWork.Notifications);
		unitOfWork.UnitOfWork.Verify(work => work.Save(), Times.Once);
	}

	[Fact]
	public async Task NotifyOrderEventAsync_TreatsConcurrentUniqueInsertAsDuplicate()
	{
		var unitOfWork = new TestUnitOfWork(new OrderHeader { Id = 42, ApplicationUserId = "customer-1" })
		{
			SaveException = new DbUpdateException("Unique constraint violation.", new InvalidOperationException()),
			DuplicateAfterFailedSave = new UserNotification
			{
				ApplicationUserId = "customer-1",
				OrderId = 42,
				Type = OrderNotificationType.PaymentFailed
			}
		};
		var logger = new Mock<ILogger<OrderNotificationService>>();
		var service = new OrderNotificationService(unitOfWork.UnitOfWork.Object, CreateUserManager().Object, logger.Object);

		await service.NotifyOrderEventAsync(42, OrderNotificationType.PaymentFailed);

		Assert.Single(unitOfWork.Notifications);
		Assert.Same(unitOfWork.DuplicateAfterFailedSave, unitOfWork.Notifications[0]);
		logger.Verify(log => log.Log(
			LogLevel.Error,
			It.IsAny<EventId>(),
			It.IsAny<It.IsAnyType>(),
			It.IsAny<Exception?>(),
			It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
	}

	[Fact]
	public async Task NotifyOrderEventAsync_LogsPersistenceFailureWithoutThrowing()
	{
		var unitOfWork = new TestUnitOfWork(new OrderHeader { Id = 42, ApplicationUserId = "customer-1" })
		{
			SaveException = new InvalidOperationException("database unavailable")
		};
		var logger = new Mock<ILogger<OrderNotificationService>>();
		var service = new OrderNotificationService(unitOfWork.UnitOfWork.Object, CreateUserManager().Object, logger.Object);

		var exception = await Record.ExceptionAsync(() => service.NotifyOrderEventAsync(42, OrderNotificationType.PaymentApproved));

		Assert.Null(exception);
		Assert.Empty(unitOfWork.Notifications);
		logger.Verify(log => log.Log(
			LogLevel.Error,
			It.IsAny<EventId>(),
			It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Failed to persist", StringComparison.Ordinal)),
			It.IsAny<Exception?>(),
			It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
	}

	[Fact]
	public void ServiceStartsAndResolvesInTestHost()
	{
		using var factory = new CustomWebApplicationFactory();
		using var client = factory.CreateClient();
		using var scope = factory.Services.CreateScope();

		Assert.IsType<OrderNotificationService>(scope.ServiceProvider.GetRequiredService<IOrderNotificationService>());
	}

	private static Mock<UserManager<ApplicationUser>> CreateUserManager(params ApplicationUser[] admins)
	{
		var manager = new Mock<UserManager<ApplicationUser>>(
			Mock.Of<IUserStore<ApplicationUser>>(),
			Options.Create(new IdentityOptions()),
			new PasswordHasher<ApplicationUser>(),
			Array.Empty<IUserValidator<ApplicationUser>>(),
			Array.Empty<IPasswordValidator<ApplicationUser>>(),
			new UpperInvariantLookupNormalizer(),
			new IdentityErrorDescriber(),
			Mock.Of<IServiceProvider>(),
			NullLogger<UserManager<ApplicationUser>>.Instance);

		manager.Setup(userManager => userManager.GetUsersInRoleAsync(SD.Role_Admin))
			.ReturnsAsync((IList<ApplicationUser>)admins);
		return manager;
	}

	private sealed class TestUnitOfWork
	{
		public Mock<IUnitOfWork> UnitOfWork { get; } = new();
		public Mock<IOrderHeaderRepository> OrderHeader { get; } = new();
		public Mock<IUserNotificationRepository> UserNotification { get; } = new();
		public List<UserNotification> Notifications { get; } = [];
		public Exception? SaveException { get; init; }
		public UserNotification? DuplicateAfterFailedSave { get; init; }

		public TestUnitOfWork(OrderHeader? order)
		{
			OrderHeader.Setup(repository => repository.Get(
				It.IsAny<Expression<Func<OrderHeader, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns((Expression<Func<OrderHeader, bool>> filter, string? _, bool _) =>
					order is not null && filter.Compile()(order) ? order : null);
			UserNotification.Setup(repository => repository.Get(
				It.IsAny<Expression<Func<UserNotification, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns((Expression<Func<UserNotification, bool>> filter, string? _, bool _) =>
					Notifications.SingleOrDefault(filter.Compile()));
			UserNotification.Setup(repository => repository.Add(It.IsAny<UserNotification>()))
				.Callback<UserNotification>(Notifications.Add);
			UserNotification.Setup(repository => repository.Remove(It.IsAny<UserNotification>()))
				.Callback<UserNotification>(notification => Notifications.Remove(notification));
			UnitOfWork.SetupGet(work => work.OrderHeader).Returns(OrderHeader.Object);
			UnitOfWork.SetupGet(work => work.UserNotification).Returns(UserNotification.Object);
			UnitOfWork.Setup(work => work.Save()).Callback(() =>
			{
				if (SaveException is null)
				{
					return;
				}

				if (DuplicateAfterFailedSave is not null)
				{
					Notifications.Add(DuplicateAfterFailedSave);
				}

				throw SaveException;
			});
		}
	}
}
