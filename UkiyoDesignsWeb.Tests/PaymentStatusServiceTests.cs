using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models;
using UkiyoDesigns.Utility;
using UkiyoDesignsWeb.Services.Payments;

namespace UkiyoDesignsWeb.Tests
{
	public class PaymentStatusServiceTests
	{
		[Fact]
		public void MarkCheckoutSessionPaid_UpdatesStripeIdsAndApprovesCustomerOrder()
		{
			var order = new OrderHeader
			{
				Id = 42,
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
