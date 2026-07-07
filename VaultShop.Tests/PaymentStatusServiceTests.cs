using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Utility;
using VaultShop.Web.Services.Payments;

namespace VaultShop.Web.Tests
{
	public class PaymentStatusServiceTests
	{
		[Fact]
		public void MarkCheckoutSessionPaid_UpdatesStripeIdsAndApprovesCustomerOrder()
		{
			var order = new OrderHeader
			{
				Id = 42,
				SessionId = "cs_test_paid",
				PaymentStatus = SD.PaymentStatusPending,
				OrderStatus = SD.StatusPending
			};
			var unitOfWork = CreateUnitOfWork(order);
			var service = new PaymentStatusService(unitOfWork.Mock.Object, NullLogger<PaymentStatusService>.Instance);

			var result = service.MarkCheckoutSessionPaid(new PaymentSessionStatusUpdate(42, "cs_test_paid", "pi_test_paid"));

			Assert.True(result);
			unitOfWork.OrderHeaderMock.Verify(x => x.UpdateStripePaymentId(42, "cs_test_paid", "pi_test_paid"), Times.Once);
			unitOfWork.OrderHeaderMock.Verify(x => x.UpdateStatus(42, SD.StatusApproved, SD.PaymentStatusApproved), Times.Once);
			unitOfWork.Mock.Verify(x => x.Save(), Times.Once);
		}

		[Fact]
		public void MarkCheckoutSessionPaid_PreservesDelayedPaymentOrderStatus()
		{
			var order = new OrderHeader
			{
				Id = 42,
				SessionId = "cs_test_paid",
				PaymentStatus = SD.PaymentStatusDelayedPayment,
				OrderStatus = SD.StatusInProcess
			};
			var unitOfWork = CreateUnitOfWork(order);
			var service = new PaymentStatusService(unitOfWork.Mock.Object, NullLogger<PaymentStatusService>.Instance);

			var result = service.MarkCheckoutSessionPaid(new PaymentSessionStatusUpdate(42, "cs_test_paid", "pi_test_paid"));

			Assert.True(result);
			unitOfWork.OrderHeaderMock.Verify(x => x.UpdateStatus(42, SD.StatusInProcess, SD.PaymentStatusApproved), Times.Once);
			unitOfWork.Mock.Verify(x => x.Save(), Times.Once);
		}

		[Fact]
		public void MarkCheckoutSessionPaid_IgnoresCancelledOrder()
		{
			var order = new OrderHeader
			{
				Id = 42,
				SessionId = "cs_test_paid",
				PaymentStatus = SD.StatusCancelled,
				OrderStatus = SD.StatusCancelled
			};
			var unitOfWork = CreateUnitOfWork(order);
			var service = new PaymentStatusService(unitOfWork.Mock.Object, NullLogger<PaymentStatusService>.Instance);

			var result = service.MarkCheckoutSessionPaid(new PaymentSessionStatusUpdate(42, "cs_test_paid", "pi_test_paid"));

			Assert.False(result);
			unitOfWork.OrderHeaderMock.Verify(x => x.UpdateStripePaymentId(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
			unitOfWork.OrderHeaderMock.Verify(x => x.UpdateStatus(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
			unitOfWork.Mock.Verify(x => x.Save(), Times.Never);
		}

		[Fact]
		public void MarkCheckoutSessionPaid_IgnoresStaleSession()
		{
			var order = new OrderHeader
			{
				Id = 42,
				SessionId = "cs_current",
				PaymentStatus = SD.PaymentStatusPending,
				OrderStatus = SD.StatusPending
			};
			var unitOfWork = CreateUnitOfWork(order);
			var service = new PaymentStatusService(unitOfWork.Mock.Object, NullLogger<PaymentStatusService>.Instance);

			var result = service.MarkCheckoutSessionPaid(new PaymentSessionStatusUpdate(42, "cs_old", "pi_test_paid"));

			Assert.False(result);
			unitOfWork.OrderHeaderMock.Verify(x => x.UpdateStripePaymentId(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
			unitOfWork.OrderHeaderMock.Verify(x => x.UpdateStatus(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
			unitOfWork.Mock.Verify(x => x.Save(), Times.Never);
		}

		[Fact]
		public void MarkCheckoutSessionPaid_IgnoresDuplicatePaidSession()
		{
			var order = new OrderHeader
			{
				Id = 42,
				SessionId = "cs_test_paid",
				PaymentStatus = SD.PaymentStatusApproved,
				OrderStatus = SD.StatusApproved
			};
			var unitOfWork = CreateUnitOfWork(order);
			var service = new PaymentStatusService(unitOfWork.Mock.Object, NullLogger<PaymentStatusService>.Instance);

			var result = service.MarkCheckoutSessionPaid(new PaymentSessionStatusUpdate(42, "cs_test_paid", "pi_test_paid"));

			Assert.True(result);
			unitOfWork.OrderHeaderMock.Verify(x => x.UpdateStripePaymentId(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
			unitOfWork.OrderHeaderMock.Verify(x => x.UpdateStatus(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
			unitOfWork.Mock.Verify(x => x.Save(), Times.Never);
		}

		[Fact]
		public void MarkCheckoutSessionFailed_UpdatesStripeIdsAndRejectsOrder()
		{
			var order = new OrderHeader
			{
				Id = 42,
				PaymentStatus = SD.PaymentStatusPending,
				OrderStatus = SD.StatusPending
			};
			var unitOfWork = CreateUnitOfWork(order);
			var service = new PaymentStatusService(unitOfWork.Mock.Object, NullLogger<PaymentStatusService>.Instance);

			var result = service.MarkCheckoutSessionFailed(new PaymentSessionStatusUpdate(42, "cs_test_failed", "pi_test_failed"));

			Assert.True(result);
			unitOfWork.OrderHeaderMock.Verify(x => x.UpdateStripePaymentId(42, "cs_test_failed", "pi_test_failed"), Times.Once);
			unitOfWork.OrderHeaderMock.Verify(x => x.UpdateStatus(42, SD.StatusCancelled, SD.PaymentStatusRejected), Times.Once);
			unitOfWork.Mock.Verify(x => x.Save(), Times.Once);
		}

		[Fact]
		public void ApproveManualBankTransfer_ApprovesPendingOrder()
		{
			var order = new OrderHeader
			{
				Id = 42,
				PaymentMethod = SD.PaymentMethodBankTransfer,
				PaymentStatus = SD.PaymentStatusPending,
				OrderStatus = SD.StatusPending
			};
			var unitOfWork = CreateUnitOfWork(order);
			var service = new PaymentStatusService(unitOfWork.Mock.Object, NullLogger<PaymentStatusService>.Instance);

			var result = service.ApproveManualBankTransfer(42);

			Assert.True(result);
			unitOfWork.OrderHeaderMock.Verify(x => x.UpdateStatus(42, SD.StatusApproved, SD.PaymentStatusApproved), Times.Once);
			unitOfWork.Mock.Verify(x => x.Save(), Times.Once);
		}

		[Fact]
		public void ApproveManualBankTransfer_IsIdempotentOnAlreadyApprovedOrder()
		{
			var order = new OrderHeader
			{
				Id = 42,
				PaymentMethod = SD.PaymentMethodBankTransfer,
				PaymentStatus = SD.PaymentStatusApproved,
				OrderStatus = SD.StatusApproved
			};
			var unitOfWork = CreateUnitOfWork(order);
			var service = new PaymentStatusService(unitOfWork.Mock.Object, NullLogger<PaymentStatusService>.Instance);

			var result = service.ApproveManualBankTransfer(42);

			Assert.True(result);
			unitOfWork.OrderHeaderMock.Verify(x => x.UpdateStatus(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
			unitOfWork.Mock.Verify(x => x.Save(), Times.Never);
		}

		[Fact]
		public void ApproveManualBankTransfer_RejectsTerminalOrder()
		{
			var order = new OrderHeader
			{
				Id = 42,
				PaymentMethod = SD.PaymentMethodBankTransfer,
				PaymentStatus = SD.StatusCancelled,
				OrderStatus = SD.StatusCancelled
			};
			var unitOfWork = CreateUnitOfWork(order);
			var service = new PaymentStatusService(unitOfWork.Mock.Object, NullLogger<PaymentStatusService>.Instance);

			var result = service.ApproveManualBankTransfer(42);

			Assert.False(result);
			unitOfWork.OrderHeaderMock.Verify(x => x.UpdateStatus(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
			unitOfWork.Mock.Verify(x => x.Save(), Times.Never);
		}

		[Fact]
		public void ApproveManualBankTransfer_RejectsNonBankTransferOrder()
		{
			var order = new OrderHeader
			{
				Id = 42,
				PaymentMethod = SD.PaymentMethodStripe,
				PaymentStatus = SD.PaymentStatusPending,
				OrderStatus = SD.StatusPending
			};
			var unitOfWork = CreateUnitOfWork(order);
			var service = new PaymentStatusService(unitOfWork.Mock.Object, NullLogger<PaymentStatusService>.Instance);

			var result = service.ApproveManualBankTransfer(42);

			Assert.False(result);
			unitOfWork.OrderHeaderMock.Verify(x => x.UpdateStatus(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
			unitOfWork.Mock.Verify(x => x.Save(), Times.Never);
		}

		private static TestUnitOfWork CreateUnitOfWork(OrderHeader orderHeader)
		{
			var testUnitOfWork = new TestUnitOfWork();
			testUnitOfWork.OrderHeaderMock
				.Setup(x => x.Get(
					It.IsAny<Expression<Func<OrderHeader, bool>>>(),
					It.IsAny<string?>(),
					It.IsAny<bool>()))
				.Returns((Expression<Func<OrderHeader, bool>> filter, string? _, bool _) =>
					new[] { orderHeader }.SingleOrDefault(filter.Compile()));

			testUnitOfWork.Mock.Setup(x => x.OrderHeader).Returns(testUnitOfWork.OrderHeaderMock.Object);
			return testUnitOfWork;
		}

		private sealed class TestUnitOfWork
		{
			public Mock<IUnitOfWork> Mock { get; } = new();
			public Mock<IOrderHeaderRepository> OrderHeaderMock { get; } = new();
		}
	}
}
