using System.Globalization;
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
		public async Task TrySendPaymentReceiptAsync_UsesTrackedOrderAndSecondCallSkipsSending()
		{
			var order = new OrderHeader
			{
				Id = 42,
				Name = "Ada",
				OrderTotal = 1500m,
				ApplicationUser = new ApplicationUser { Email = "ada@vaultshop.test" }
			};
			var test = CreateService(order);
			test.EmailSenderMock
				.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
				.Returns(Task.CompletedTask);

			await test.Service.TrySendPaymentReceiptAsync(42);
			var sentAt = order.PaymentReceiptEmailSentUtc;
			await test.Service.TrySendPaymentReceiptAsync(42);

			Assert.NotNull(sentAt);
			Assert.Equal(sentAt, order.PaymentReceiptEmailSentUtc);
			test.OrderHeaderMock.Verify(x => x.Get(
				It.IsAny<Expression<Func<OrderHeader, bool>>>(), "ApplicationUser", true), Times.Exactly(2));
			test.EmailSenderMock.Verify(x => x.SendEmailAsync("ada@vaultshop.test", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
			test.UnitOfWorkMock.Verify(x => x.Save(), Times.Once);
		}

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

		[Fact]
		public async Task TrySendOrderConfirmationAsync_PickupOrder_IncludesBranchBlockAndPolicy()
		{
			var order = new OrderHeader
			{
				Id = 42,
				Name = "Ada",
				OrderTotal = 1500m,
				PaymentMethod = SD.PaymentMethodBankTransfer,
				PaymentStatus = SD.PaymentStatusPending,
				DeliveryType = SD.DeliveryTypePickup,
				PickupAgencyName = "Sucursal Centro",
				PickupAgencyCode = "CEN01",
				PickupAgencyAddress = "Centro 1",
				PickupAgencyHours = "LUN A VIE 9 A 18",
				ApplicationUser = new ApplicationUser { Email = "ada@vaultshop.test" }
			};
			var test = CreateService(order);
			string? body = null;
			test.EmailSenderMock
				.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
				.Callback<string, string, string>((_, _, b) => body = b)
				.Returns(Task.CompletedTask);

			await test.Service.TrySendOrderConfirmationAsync(42);

			Assert.NotNull(body);
			Assert.Contains("Sucursal Centro", body);
			Assert.Contains("LUN A VIE 9 A 18", body);
			Assert.True(body.Contains("5 días hábiles") || body.Contains("5 business days"));
		}

		[Fact]
		public async Task TrySendShippingConfirmationAsync_PickupOrder_IncludesBranchBlockTrackingAndCorreoLink()
		{
			var order = new OrderHeader
			{
				Id = 42,
				Name = "Ada",
				OrderTotal = 1500m,
				TrackingNumber = "ABC123",
				Carrier = "Correo Argentino",
				DeliveryType = SD.DeliveryTypePickup,
				PickupAgencyName = "Sucursal Centro",
				PickupAgencyCode = "CEN01",
				PickupAgencyAddress = "Centro 1",
				PickupAgencyHours = "LUN A VIE 9 A 18",
				ApplicationUser = new ApplicationUser { Email = "ada@vaultshop.test" }
			};
			var test = CreateService(order);
			string? body = null;
			test.EmailSenderMock
				.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
				.Callback<string, string, string>((_, _, b) => body = b)
				.Returns(Task.CompletedTask);

			await test.Service.TrySendShippingConfirmationAsync(42);

			Assert.NotNull(body);
			Assert.Contains("Sucursal Centro", body);
			Assert.Contains("ABC123", body);
			Assert.Contains("correoargentino.com.ar/seguimiento-de-envios", body);
			Assert.NotNull(order.ShippingConfirmationEmailSentUtc);
		}

		[Theory]
		[InlineData("es-AR", "Retiro en sucursal", "Horario", "5 días hábiles")]
		[InlineData("en-US", "Branch pickup", "Hours", "5 business days")]
		public void OrderConfirmation_PickupBlock_RendersInBothCultures(
			string cultureName, string title, string hoursLabel, string policyFragment)
		{
			var content = EmailTemplates.OrderConfirmation(
				"Shop", "Ada", 7, [], "$100", "https://x.test", new CultureInfo(cultureName),
				deliveryType: SD.DeliveryTypePickup, pickupAgencyName: "Sucursal Centro",
				pickupAgencyCode: "CEN01", pickupAgencyAddress: "Centro 1",
				pickupAgencyHours: "LUN A VIE 9 A 18");

			Assert.Contains(title, content.Body);
			Assert.Contains("Sucursal Centro", content.Body);
			Assert.Contains($"{hoursLabel}:</strong> LUN A VIE 9 A 18", content.Body);
			Assert.Contains(policyFragment, content.Body);
		}

		[Theory]
		[InlineData("es-AR")]
		[InlineData("en-US")]
		public void OrderConfirmation_PickupBlock_UsesSentinelWhenHoursMissing(string cultureName)
		{
			var content = EmailTemplates.OrderConfirmation(
				"Shop", "Ada", 7, [], "$100", "https://x.test", new CultureInfo(cultureName),
				deliveryType: SD.DeliveryTypePickup, pickupAgencyName: "Sucursal Centro",
				pickupAgencyHours: null);

			Assert.Contains(PostalAgency.HoursUnknown, content.Body);
		}

		[Fact]
		public void OrderConfirmation_NonPickupOrder_HasNoBranchBlock()
		{
			var content = EmailTemplates.OrderConfirmation(
				"Shop", "Ada", 7, [], "$100", "https://x.test", new CultureInfo("es-AR"));

			Assert.DoesNotContain("Retiro en sucursal", content.Body);
			Assert.DoesNotContain("Branch pickup", content.Body);
		}

		[Theory]
		[InlineData("es-AR", "en camino a la sucursal", "Código de seguimiento:")]
		[InlineData("en-US", "on its way to the", "Tracking number:")]
		public void ShippingConfirmation_PickupOrder_UsesInTransitCopy(
			string cultureName, string inTransitFragment, string trackingLabel)
		{
			var content = EmailTemplates.ShippingConfirmation(
				"Shop", "Ada", 7, "ABC123", "Correo Argentino", "https://x.test",
				new CultureInfo(cultureName), SD.DeliveryTypePickup, "Sucursal Centro",
				"CEN01", "Centro 1", "LUN A VIE 9 A 18");

			Assert.Contains(inTransitFragment, content.Body);
			Assert.Contains("Sucursal Centro", content.Body);
			Assert.Contains("LUN A VIE 9 A 18", content.Body);
			Assert.Contains($"{trackingLabel} ABC123", content.Body);
			Assert.Contains("correoargentino.com.ar/seguimiento-de-envios", content.Body);
		}

		[Fact]
		public void ShippingConfirmation_HtmlEncodesTrackingAndCarrier()
		{
			var content = EmailTemplates.ShippingConfirmation(
				"Shop", "Ada", 7, "<b>ABC</b>", "<i>Correo</i>", "https://x.test",
				new CultureInfo("es-AR"));

			Assert.Contains("&lt;b&gt;ABC&lt;/b&gt;", content.Body);
			Assert.Contains("&lt;i&gt;Correo&lt;/i&gt;", content.Body);
			Assert.DoesNotContain("<b>ABC</b>", content.Body);
		}

		[Fact]
		public void ShippingConfirmation_NonPickupOrder_KeepsCurrentCopy()
		{
			var content = EmailTemplates.ShippingConfirmation(
				"Shop", "Ada", 7, "ABC123", "Correo Argentino", "https://x.test",
				new CultureInfo("es-AR"));

			Assert.Contains("ha sido despachado", content.Body);
			Assert.DoesNotContain("Retiro en sucursal", content.Body);
			Assert.DoesNotContain("correoargentino.com.ar", content.Body);
		}

		private static TestContext CreateService(OrderHeader order, IEnumerable<OrderDetail>? details = null)
		{
			var orderHeaderMock = new Mock<IOrderHeaderRepository>();
			orderHeaderMock
				.Setup(x => x.TryClaimOrderConfirmationEmail(It.IsAny<int>()))
				.Returns(true);
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

			return new TestContext(service, unitOfWorkMock, orderHeaderMock, emailSenderMock);
		}

		private sealed record TestContext(
			TransactionalEmailService Service,
			Mock<IUnitOfWork> UnitOfWorkMock,
			Mock<IOrderHeaderRepository> OrderHeaderMock,
			Mock<IEmailSender> EmailSenderMock);
	}
}
