using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models;
using UkiyoDesigns.Models.ViewModels;
using UkiyoDesigns.Utility;
using UkiyoDesignsWeb.Areas.Admin.Controllers;
using UkiyoDesignsWeb.Services.Payments;

namespace UkiyoDesignsWeb.Tests
{
	public class OrderControllerManualPaymentApprovalTests
	{
		[Fact]
		public void ApprovePaymentDevelopment_ReturnsNotFound_WhenNotDevelopment()
		{
			var test = CreateController(Environments.Production, allowManualApproval: true);

			var result = test.Controller.ApprovePaymentDevelopment();

			Assert.IsType<NotFoundResult>(result);
			test.PaymentStatusMock.Verify(x => x.MarkCheckoutSessionPaid(It.IsAny<PaymentSessionStatusUpdate>()), Times.Never);
		}

		[Fact]
		public void ApprovePaymentDevelopment_ReturnsNotFound_WhenConfigDisabled()
		{
			var test = CreateController(Environments.Development, allowManualApproval: false);

			var result = test.Controller.ApprovePaymentDevelopment();

			Assert.IsType<NotFoundResult>(result);
			test.PaymentStatusMock.Verify(x => x.MarkCheckoutSessionPaid(It.IsAny<PaymentSessionStatusUpdate>()), Times.Never);
		}

		[Fact]
		public void ApprovePaymentDevelopment_UsesStoredCheckoutSession_WhenEnabled()
		{
			var order = new OrderHeader
			{
				Id = 42,
				SessionId = "cs_test_manual",
				PaymentIntentId = "pi_existing",
				PaymentStatus = SD.PaymentStatusPending,
				OrderStatus = SD.StatusPending
			};
			var test = CreateController(Environments.Development, allowManualApproval: true, order);
			test.PaymentStatusMock
				.Setup(x => x.MarkCheckoutSessionPaid(It.IsAny<PaymentSessionStatusUpdate>()))
				.Returns(true);

			var result = test.Controller.ApprovePaymentDevelopment();

			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal("Details", redirect.ActionName);
			Assert.Equal(42, redirect.RouteValues?["orderId"]);
			test.PaymentStatusMock.Verify(x => x.MarkCheckoutSessionPaid(
				It.Is<PaymentSessionStatusUpdate>(update =>
					update.OrderId == 42 &&
					update.SessionId == "cs_test_manual" &&
					update.PaymentIntentId == "pi_existing")), Times.Once);
		}

		[Fact]
		public void ApprovePaymentDevelopment_ReturnsNotFound_WhenOrderHasNoCheckoutSession()
		{
			var order = new OrderHeader
			{
				Id = 42,
				PaymentStatus = SD.PaymentStatusPending,
				OrderStatus = SD.StatusPending
			};
			var test = CreateController(Environments.Development, allowManualApproval: true, order);

			var result = test.Controller.ApprovePaymentDevelopment();

			Assert.IsType<NotFoundResult>(result);
			test.PaymentStatusMock.Verify(x => x.MarkCheckoutSessionPaid(It.IsAny<PaymentSessionStatusUpdate>()), Times.Never);
		}

		[Fact]
		public void StartProcessing_DoesNotProcessUnpaidCustomerOrder()
		{
			var order = new OrderHeader
			{
				Id = 42,
				PaymentStatus = SD.PaymentStatusPending,
				OrderStatus = SD.StatusApproved
			};
			var test = CreateController(Environments.Development, allowManualApproval: false, order);

			var result = test.Controller.StartProcessing();

			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal("Details", redirect.ActionName);
			Assert.Equal(42, redirect.RouteValues?["orderId"]);
			test.OrderHeaderMock.Verify(x => x.UpdateStatus(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
			test.UnitOfWorkMock.Verify(x => x.Save(), Times.Never);
		}

		[Fact]
		public void StartProcessing_ProcessesApprovedPaidCustomerOrder()
		{
			var order = new OrderHeader
			{
				Id = 42,
				PaymentStatus = SD.PaymentStatusApproved,
				OrderStatus = SD.StatusApproved
			};
			var test = CreateController(Environments.Development, allowManualApproval: false, order);

			var result = test.Controller.StartProcessing();

			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal("Details", redirect.ActionName);
			Assert.Equal(42, redirect.RouteValues?["orderId"]);
			test.OrderHeaderMock.Verify(x => x.UpdateStatus(42, SD.StatusInProcess, null), Times.Once);
			test.UnitOfWorkMock.Verify(x => x.Save(), Times.Once);
		}

		[Fact]
		public void StartProcessing_ProcessesApprovedDelayedPaymentOrder()
		{
			var order = new OrderHeader
			{
				Id = 42,
				CompanyId = 7,
				PaymentStatus = SD.PaymentStatusDelayedPayment,
				OrderStatus = SD.StatusApproved
			};
			var test = CreateController(Environments.Development, allowManualApproval: false, order);

			var result = test.Controller.StartProcessing();

			Assert.IsType<RedirectToActionResult>(result);
			test.OrderHeaderMock.Verify(x => x.UpdateStatus(42, SD.StatusInProcess, null), Times.Once);
			test.UnitOfWorkMock.Verify(x => x.Save(), Times.Once);
		}

		[Fact]
		public void DetailsPayNow_CreatesSession_ForCompanyDelayedPaymentBeforeShipping()
		{
			var order = new OrderHeader
			{
				Id = 42,
				ApplicationUserId = "company-user",
				CompanyId = 7,
				PaymentStatus = SD.PaymentStatusDelayedPayment,
				OrderStatus = SD.StatusApproved
			};
			var detail = new OrderDetail
			{
				OrderHeaderId = 42,
				Product = new Product { Name = "Test Product" },
				Price = 100m,
				Count = 2
			};
			var test = CreateController(
				Environments.Development,
				allowManualApproval: false,
				orderHeader: order,
				orderDetails: [detail],
				currentUser: new ApplicationUser { Id = "company-user", CompanyId = 7 },
				user: CreateUser("company-user", SD.Role_Company));
			test.PaymentSessionMock
				.Setup(x => x.CreateCheckoutSession(It.IsAny<PaymentSessionRequest>()))
				.Returns(new PaymentSessionResult("cs_test_pay_now", "pi_test_pay_now", "https://stripe.test/pay"));

			var result = test.Controller.Details_PAY_NOW(42);

			var status = Assert.IsType<StatusCodeResult>(result);
			Assert.Equal(303, status.StatusCode);
			Assert.Equal("https://stripe.test/pay", test.Controller.Response.Headers["Location"].ToString());
			test.PaymentSessionMock.Verify(x => x.CreateCheckoutSession(
				It.Is<PaymentSessionRequest>(request =>
					request.OrderId == 42 &&
					request.LineItems.Single().ProductName == "Test Product" &&
					request.LineItems.Single().UnitPrice == 100m &&
					request.LineItems.Single().Quantity == 2)), Times.Once);
			test.OrderHeaderMock.Verify(x => x.UpdateStripePaymentId(42, "cs_test_pay_now", "pi_test_pay_now"), Times.Once);
			test.UnitOfWorkMock.Verify(x => x.Save(), Times.Once);
		}

		private static TestController CreateController(
			string environmentName,
			bool allowManualApproval,
			OrderHeader? orderHeader = null,
			IEnumerable<OrderDetail>? orderDetails = null,
			ApplicationUser? currentUser = null,
			ClaimsPrincipal? user = null)
		{
			var unitOfWorkMock = new Mock<IUnitOfWork>();
			var orderHeaderMock = new Mock<IOrderHeaderRepository>();
			var orderDetailMock = new Mock<IOrderDetailRepository>();
			var applicationUserMock = new Mock<IApplicationUserRepository>();
			if (orderHeader is not null)
			{
				orderHeaderMock
					.Setup(x => x.Get(
						It.IsAny<Expression<Func<OrderHeader, bool>>>(),
						It.IsAny<string?>(),
						It.IsAny<bool>()))
					.Returns((Expression<Func<OrderHeader, bool>> filter, string? _, bool _) =>
						new[] { orderHeader }.SingleOrDefault(filter.Compile()));
			}

			var orderDetailList = (orderDetails ?? []).ToList();
			orderDetailMock
				.Setup(x => x.GetAll(
					It.IsAny<Expression<Func<OrderDetail, bool>>>(),
					It.IsAny<string?>(),
					It.IsAny<bool>()))
				.Returns((Expression<Func<OrderDetail, bool>> filter, string? _, bool _) =>
					orderDetailList.Where(filter.Compile()).ToList());

			if (currentUser is not null)
			{
				applicationUserMock
					.Setup(x => x.Get(
						It.IsAny<Expression<Func<ApplicationUser, bool>>>(),
						It.IsAny<string?>(),
						It.IsAny<bool>()))
					.Returns((Expression<Func<ApplicationUser, bool>> filter, string? _, bool _) =>
						new[] { currentUser }.SingleOrDefault(filter.Compile()));
			}

			unitOfWorkMock.Setup(x => x.OrderHeader).Returns(orderHeaderMock.Object);
			unitOfWorkMock.Setup(x => x.OrderDetail).Returns(orderDetailMock.Object);
			unitOfWorkMock.Setup(x => x.ApplicationUser).Returns(applicationUserMock.Object);

			var paymentStatusMock = new Mock<IPaymentStatusService>();
			var paymentSessionMock = new Mock<IPaymentSessionService>();
			var localizerMock = new Mock<IStringLocalizer<OrderController>>();
			localizerMock
				.Setup(x => x[It.IsAny<string>()])
				.Returns((string name) => new LocalizedString(name, name));
			var environmentMock = new Mock<IWebHostEnvironment>();
			environmentMock.SetupGet(x => x.EnvironmentName).Returns(environmentName);
			var configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["Payments:AllowDevelopmentManualApproval"] = allowManualApproval.ToString()
				})
				.Build();

			var httpContext = new DefaultHttpContext
			{
				User = user ?? new ClaimsPrincipal(new ClaimsIdentity())
			};
			httpContext.Request.Scheme = "https";
			httpContext.Request.Host = new HostString("vaultshop.test");

			var controller = new OrderController(
				unitOfWorkMock.Object,
				localizerMock.Object,
				NullLogger<OrderController>.Instance,
				paymentSessionMock.Object,
				paymentStatusMock.Object,
				environmentMock.Object,
				configuration)
			{
				OrderVM = new OrderVM { OrderHeader = new OrderHeader { Id = orderHeader?.Id ?? 42 } },
				ControllerContext = new ControllerContext { HttpContext = httpContext },
				TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
			};

			return new TestController(controller, paymentStatusMock, paymentSessionMock, unitOfWorkMock, orderHeaderMock);
		}

		private static ClaimsPrincipal CreateUser(string userId, params string[] roles)
		{
			var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
			claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
			return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
		}

		private sealed record TestController(OrderController Controller, Mock<IPaymentStatusService> PaymentStatusMock, Mock<IPaymentSessionService> PaymentSessionMock, Mock<IUnitOfWork> UnitOfWorkMock, Mock<IOrderHeaderRepository> OrderHeaderMock);
	}
}
