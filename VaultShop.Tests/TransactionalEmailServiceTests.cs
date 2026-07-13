using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Web.Services.Branding;
using VaultShop.Web.Services.Email;

namespace VaultShop.Web.Tests
{
	public class TransactionalEmailServiceTests
	{
		[Fact]
		public async Task TrySendAdminBankTransferConfirmationRequestAsync_SendsOnceAndStoresTimestamp()
		{
			var order = new OrderHeader
			{
				Id = 42,
				Name = "Ada",
				OrderTotal = 1500m,
				TransferConfirmedByCustomerAt = new DateTime(2026, 7, 12, 12, 0, 0, DateTimeKind.Utc)
			};
			var test = CreateService(order);

			await test.Service.TrySendAdminBankTransferConfirmationRequestAsync(42);

			Assert.NotNull(order.AdminBankTransferAlertEmailSentUtc);
			test.EmailSenderMock.Verify(x => x.SendEmailAsync("admin@vaultshop.test", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
			test.UnitOfWorkMock.Verify(x => x.Save(), Times.Once);
		}

		[Fact]
		public async Task TrySendAdminBankTransferConfirmationRequestAsync_IsIdempotent_WhenAlreadySent()
		{
			var sentAt = new DateTime(2026, 7, 12, 13, 0, 0, DateTimeKind.Utc);
			var order = new OrderHeader
			{
				Id = 42,
				Name = "Ada",
				OrderTotal = 1500m,
				TransferConfirmedByCustomerAt = new DateTime(2026, 7, 12, 12, 0, 0, DateTimeKind.Utc),
				AdminBankTransferAlertEmailSentUtc = sentAt
			};
			var test = CreateService(order);

			await test.Service.TrySendAdminBankTransferConfirmationRequestAsync(42);

			Assert.Equal(sentAt, order.AdminBankTransferAlertEmailSentUtc);
			test.EmailSenderMock.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
			test.UnitOfWorkMock.Verify(x => x.Save(), Times.Never);
		}

		[Fact]
		public async Task TrySendAdminBankTransferConfirmationRequestAsync_DoesNothing_WhenCustomerHasNotConfirmed()
		{
			var order = new OrderHeader
			{
				Id = 42,
				Name = "Ada",
				OrderTotal = 1500m
			};
			var test = CreateService(order);

			await test.Service.TrySendAdminBankTransferConfirmationRequestAsync(42);

			Assert.Null(order.AdminBankTransferAlertEmailSentUtc);
			test.EmailSenderMock.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
			test.UnitOfWorkMock.Verify(x => x.Save(), Times.Never);
		}

		private static TestContext CreateService(OrderHeader order)
		{
			var orderHeaderMock = new Mock<IOrderHeaderRepository>();
			orderHeaderMock
				.Setup(x => x.Get(
					It.IsAny<Expression<Func<OrderHeader, bool>>>(),
					It.IsAny<string?>(),
					It.IsAny<bool>()))
				.Returns((Expression<Func<OrderHeader, bool>> filter, string? _, bool _) =>
					new[] { order }.SingleOrDefault(filter.Compile()));

			var unitOfWorkMock = new Mock<IUnitOfWork>();
			unitOfWorkMock.Setup(x => x.OrderHeader).Returns(orderHeaderMock.Object);

			var emailSenderMock = new Mock<IEmailSender>();
			var configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["Email:AdminEmail"] = "admin@vaultshop.test"
				})
				.Build();

			var service = new TransactionalEmailService(
				unitOfWorkMock.Object,
				emailSenderMock.Object,
				Options.Create(new BrandingOptions { PublicName = "VaultShop" }),
				configuration,
				NullLogger<TransactionalEmailService>.Instance);

			return new TestContext(service, unitOfWorkMock, emailSenderMock);
		}

		private sealed record TestContext(
			TransactionalEmailService Service,
			Mock<IUnitOfWork> UnitOfWorkMock,
			Mock<IEmailSender> EmailSenderMock);
	}
}
