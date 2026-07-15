using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Utility;
using VaultShop.Web.Controllers;
using VaultShop.Web.Services.Email;
using VaultShop.Web.Services.Payments;

namespace VaultShop.Web.Tests
{
	public class MercadoPagoWebhookControllerTests
	{
		[Fact]
		public async Task Post_ValidSignatureAndApprovedPayment_MarksPaidAndSendsReceipt()
		{
			using var test = CreateController(MercadoPagoOrder(), new("", "PAYMENT123", "paid", "42", 100m));
			test.PaymentStatus.Setup(service => service.MarkCheckoutSessionPaid(It.IsAny<PaymentSessionStatusUpdate>())).Returns(true);
			SignRequest(test.Controller, "PAYMENT123");

			var result = await test.Controller.Post();

			Assert.IsType<OkResult>(result);
			test.PaymentStatus.Verify(service => service.MarkCheckoutSessionPaid(
				It.Is<PaymentSessionStatusUpdate>(update => update.OrderId == 42 && update.SessionId == "pref_123" && update.PaymentIntentId == "PAYMENT123")), Times.Once);
			test.Email.Verify(service => service.TrySendPaymentReceiptAsync(42), Times.Once);
		}

		[Fact]
		public async Task Post_InvalidSignature_ReturnsBadRequestWithoutLookupOrStateChange()
		{
			using var test = CreateController(MercadoPagoOrder(), new("", "PAYMENT123", "paid", "42", 100m));
			test.Controller.Request.QueryString = new QueryString("?data.id=PAYMENT123");
			test.Controller.Request.Headers["x-request-id"] = "request-123";
			test.Controller.Request.Headers["x-signature"] = "ts=1700000000,v1=invalid";

			var result = await test.Controller.Post();

			Assert.IsType<BadRequestResult>(result);
			test.PaymentSession.Verify(service => service.GetCheckoutSessionStatus(It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
			test.PaymentStatus.Verify(service => service.MarkCheckoutSessionPaid(It.IsAny<PaymentSessionStatusUpdate>()), Times.Never);
		}

		[Fact]
		public async Task Post_DifferentPaymentForApprovedOrder_LogsPossibleDuplicateCharge()
		{
			var order = MercadoPagoOrder();
			order.PaymentStatus = SD.PaymentStatusApproved;
			order.PaymentIntentId = "PAYMENT_OLD";
			using var test = CreateController(order, new("", "PAYMENT123", "paid", "42", 100m));
			test.PaymentStatus.Setup(service => service.MarkCheckoutSessionPaid(It.IsAny<PaymentSessionStatusUpdate>())).Returns(true);
			SignRequest(test.Controller, "PAYMENT123");

			var result = await test.Controller.Post();

			Assert.IsType<OkResult>(result);
			test.Logger.Verify(logger => logger.Log(
				LogLevel.Error,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Possible duplicate charge", StringComparison.Ordinal)),
				It.IsAny<Exception?>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
		}

		[Theory]
		[InlineData("amount")]
		[InlineData("order")]
		[InlineData("provider")]
		public async Task Post_VerifiedPaymentMismatch_AcknowledgesWithoutApproval(string mismatch)
		{
			var order = MercadoPagoOrder();
			if (mismatch == "provider") order.PaymentMethod = SD.PaymentMethodStripe;
			var externalReference = mismatch == "order" ? "99" : "42";
			var amount = mismatch == "amount" ? 99m : 100m;
			using var test = CreateController(order, new("", "PAYMENT123", "paid", externalReference, amount));
			SignRequest(test.Controller, "PAYMENT123");

			var result = await test.Controller.Post();

			Assert.IsType<OkResult>(result);
			test.PaymentStatus.Verify(service => service.MarkCheckoutSessionPaid(It.IsAny<PaymentSessionStatusUpdate>()), Times.Never);
			test.Email.Verify(service => service.TrySendPaymentReceiptAsync(It.IsAny<int>()), Times.Never);
		}

		private static OrderHeader MercadoPagoOrder() => new()
		{
			Id = 42,
			PaymentMethod = SD.PaymentMethodMercadoPago,
			PaymentStatus = SD.PaymentStatusPending,
			OrderStatus = SD.StatusPending,
			SessionId = "pref_123",
			OrderTotal = 100m
		};

		private static WebhookTest CreateController(OrderHeader order, PaymentSessionStatusResult payment)
		{
			var paymentSession = new Mock<IPaymentSessionService>();
			paymentSession.Setup(service => service.GetCheckoutSessionStatus(string.Empty, "PAYMENT123")).Returns(payment);
			var services = new ServiceCollection();
			services.AddKeyedSingleton(SD.PaymentMethodMercadoPago, paymentSession.Object);
			var serviceProvider = services.BuildServiceProvider();

			var orderRepository = new Mock<IOrderHeaderRepository>();
			orderRepository.Setup(repository => repository.Get(
				It.IsAny<Expression<Func<OrderHeader, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns((Expression<Func<OrderHeader, bool>> filter, string? _, bool _) => new[] { order }.SingleOrDefault(filter.Compile()));
			var unitOfWork = new Mock<IUnitOfWork>();
			unitOfWork.SetupGet(item => item.OrderHeader).Returns(orderRepository.Object);
			var paymentStatus = new Mock<IPaymentStatusService>();
			var email = new Mock<ITransactionalEmailService>();
			var logger = new Mock<ILogger<MercadoPagoWebhookController>>();
			var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Payments:MercadoPagoWebhookSecret"] = "webhook-secret"
			}).Build();
			var controller = new MercadoPagoWebhookController(
				configuration,
				serviceProvider,
				unitOfWork.Object,
				paymentStatus.Object,
				email.Object,
				logger.Object)
			{
				ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
			};

			return new(controller, paymentSession, paymentStatus, email, logger, serviceProvider);
		}

		private static void SignRequest(MercadoPagoWebhookController controller, string paymentId)
		{
			const string requestId = "request-123";
			const string timestamp = "1700000000";
			const string secret = "webhook-secret";
			var manifest = $"id:{paymentId.ToLowerInvariant()};request-id:{requestId};ts:{timestamp};";
			var hash = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant();
			controller.Request.QueryString = new QueryString($"?data.id={paymentId}");
			controller.Request.Headers["x-request-id"] = requestId;
			controller.Request.Headers["x-signature"] = $"ts={timestamp},v1={hash}";
		}

		private sealed record WebhookTest(
			MercadoPagoWebhookController Controller,
			Mock<IPaymentSessionService> PaymentSession,
			Mock<IPaymentStatusService> PaymentStatus,
			Mock<ITransactionalEmailService> Email,
			Mock<ILogger<MercadoPagoWebhookController>> Logger,
			ServiceProvider ServiceProvider) : IDisposable
		{
			public void Dispose() => ServiceProvider.Dispose();
		}
	}
}
