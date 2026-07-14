using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Utility;
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
		public async Task TrySendOrderConfirmationAsync_BankTransferPending_IncludesBankData()
		{
			var order = new OrderHeader
			{
				Id = 42,
				Name = "Ada",
				OrderTotal = 1500m,
				PaymentMethod = SD.PaymentMethodBankTransfer,
				PaymentStatus = SD.PaymentStatusPending,
				ApplicationUser = new ApplicationUser { Email = "ada@vaultshop.test" }
			};
			var details = new[]
			{
				new OrderDetail { OrderHeaderId = 42, ProductId = 7, Product = new Product { Name = "Bag" }, Count = 1, Price = 1500m }
			};
			var test = CreateService(order, details);
			string? subject = null;
			string? body = null;
			test.EmailSenderMock
				.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
				.Callback<string, string, string>((_, s, b) =>
				{
					subject = s;
					body = b;
				})
				.Returns(Task.CompletedTask);

			await test.Service.TrySendOrderConfirmationAsync(42);

			Assert.Contains("CBU", body);
			Assert.Contains("1234567890123456789012", body);
			Assert.Contains("vault.alias", body);
			Assert.Contains("Vault Shop", body);
			Assert.Contains("Galicia", body);
			Assert.Contains("admin/order/details?orderId=42", body);
			Assert.NotNull(subject);
			Assert.NotNull(order.OrderConfirmationEmailSentUtc);
		}

		[Fact]
		public async Task TrySendAdminNewOrderAlertAsync_IncludesPaymentMethod_AndDelayedPaymentNote()
		{
			var order = new OrderHeader
			{
				Id = 42,
				Name = "Ada",
				OrderTotal = 1500m,
				CompanyId = 7,
				PaymentStatus = SD.PaymentStatusDelayedPayment
			};
			var test = CreateService(order);
			string? body = null;
			test.EmailSenderMock
				.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
				.Callback<string, string, string>((_, _, b) => body = b)
				.Returns(Task.CompletedTask);

			await test.Service.TrySendAdminNewOrderAlertAsync(42);

			Assert.NotNull(body);
			Assert.True(body.Contains("Unspecified", StringComparison.OrdinalIgnoreCase) || body.Contains("Sin definir", StringComparison.OrdinalIgnoreCase));
			Assert.True(body.Contains("delayed payment", StringComparison.OrdinalIgnoreCase) || body.Contains("pago diferido", StringComparison.OrdinalIgnoreCase));
		}

		private static TestContext CreateService(OrderHeader order, IEnumerable<OrderDetail>? details = null)
		{
			var orderHeaderMock = new Mock<IOrderHeaderRepository>();
			orderHeaderMock
				.Setup(x => x.Get(
					It.IsAny<Expression<Func<OrderHeader, bool>>>(),
					It.IsAny<string?>(),
					It.IsAny<bool>()))
				.Returns((Expression<Func<OrderHeader, bool>> filter, string? _, bool _) =>
					new[] { order }.SingleOrDefault(filter.Compile()));

			var orderDetailList = (details ?? []).ToList();
			var orderDetailMock = new Mock<IOrderDetailRepository>();
			orderDetailMock
				.Setup(x => x.GetAll(
					It.IsAny<Expression<Func<OrderDetail, bool>>>(),
					It.IsAny<string?>(),
					It.IsAny<bool>()))
				.Returns((Expression<Func<OrderDetail, bool>> filter, string? _, bool _) =>
					orderDetailList.Where(filter.Compile()).ToList());

			var unitOfWorkMock = new Mock<IUnitOfWork>();
			unitOfWorkMock.Setup(x => x.OrderHeader).Returns(orderHeaderMock.Object);
			unitOfWorkMock.Setup(x => x.OrderDetail).Returns(orderDetailMock.Object);

			var emailSenderMock = new Mock<IEmailSender>();
			var configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["Email:AdminEmail"] = "admin@vaultshop.test",
					["Payments:BankTransferCbu"] = "1234567890123456789012",
					["Payments:BankTransferAlias"] = "vault.alias",
					["Payments:BankTransferRecipientName"] = "Vault Shop",
					["Payments:BankTransferBankName"] = "Galicia"
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