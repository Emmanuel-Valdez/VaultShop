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
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Models.ViewModels;
using VaultShop.Utility;
using VaultShop.Web.Areas.Admin.Controllers;
using VaultShop.Web.Services.Email;
using VaultShop.Web.Services.Payments;

namespace VaultShop.Web.Tests
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
		public void ConfirmBankTransfer_ApprovesOrder_AndRedirectsToDetails()
		{
			var order = new OrderHeader
			{
				Id = 42,
				PaymentMethod = SD.PaymentMethodBankTransfer,
				PaymentStatus = SD.PaymentStatusPending,
				OrderStatus = SD.StatusPending
			};
			var test = CreateController(Environments.Development, allowManualApproval: false, order);
			test.PaymentStatusMock
				.Setup(x => x.ApproveManualBankTransfer(42))
				.Returns(true);

			var result = test.Controller.ConfirmBankTransfer();

			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal("Details", redirect.ActionName);
			Assert.Equal(42, redirect.RouteValues?["orderId"]);
			test.PaymentStatusMock.Verify(x => x.ApproveManualBankTransfer(42), Times.Once);
		}

		[Fact]
		public void ConfirmBankTransfer_DoesNotSetSuccessMessage_WhenApprovalFails()
		{
			var order = new OrderHeader
			{
				Id = 42,
				PaymentMethod = SD.PaymentMethodBankTransfer,
				PaymentStatus = SD.PaymentStatusPending,
				OrderStatus = SD.StatusPending
			};
			var test = CreateController(Environments.Development, allowManualApproval: false, order);
			test.PaymentStatusMock
				.Setup(x => x.ApproveManualBankTransfer(42))
				.Returns(false);

			var result = test.Controller.ConfirmBankTransfer();

			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal("Details", redirect.ActionName);
			Assert.False(test.Controller.TempData.ContainsKey("Success"));
		}

		[Fact]
		public void ConfirmTransferSent_SetsTimestampOnce_ForOwnerBankTransferPendingOrder()
		{
			var order = new OrderHeader
			{
				Id = 42,
				ApplicationUserId = "customer-user",
				PaymentMethod = SD.PaymentMethodBankTransfer,
				PaymentStatus = SD.PaymentStatusPending,
				OrderStatus = SD.StatusPending
			};
			var test = CreateController(
				Environments.Development,
				allowManualApproval: false,
				orderHeader: order,
				user: CreateUser("customer-user"));
			var before = DateTime.UtcNow;

			var result = test.Controller.ConfirmTransferSent(42);

			var after = DateTime.UtcNow;
			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal("Details", redirect.ActionName);
			Assert.Equal(42, redirect.RouteValues?["orderId"]);
			Assert.NotNull(order.TransferConfirmedByCustomerAt);
			Assert.InRange(order.TransferConfirmedByCustomerAt!.Value, before, after);
			test.OrderHeaderMock.Verify(x => x.Update(order), Times.Once);
			test.UnitOfWorkMock.Verify(x => x.Save(), Times.Once);
		}

		[Fact]
		public void ConfirmTransferSent_IsIdempotent_WhenAlreadyConfirmed()
		{
			var confirmedAt = new DateTime(2026, 7, 12, 12, 0, 0, DateTimeKind.Utc);
			var order = new OrderHeader
			{
				Id = 42,
				ApplicationUserId = "customer-user",
				PaymentMethod = SD.PaymentMethodBankTransfer,
				PaymentStatus = SD.PaymentStatusPending,
				OrderStatus = SD.StatusPending,
				TransferConfirmedByCustomerAt = confirmedAt
			};
			var test = CreateController(
				Environments.Development,
				allowManualApproval: false,
				orderHeader: order,
				user: CreateUser("customer-user"));

			var result = test.Controller.ConfirmTransferSent(42);

			Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal(confirmedAt, order.TransferConfirmedByCustomerAt);
			test.OrderHeaderMock.Verify(x => x.Update(It.IsAny<OrderHeader>()), Times.Never);
			test.UnitOfWorkMock.Verify(x => x.Save(), Times.Never);
		}

		[Fact]
		public void ConfirmTransferSent_ReturnsNotFound_ForNonOwner()
		{
			var order = new OrderHeader
			{
				Id = 42,
				ApplicationUserId = "customer-user",
				PaymentMethod = SD.PaymentMethodBankTransfer,
				PaymentStatus = SD.PaymentStatusPending,
				OrderStatus = SD.StatusPending
			};
			var test = CreateController(
				Environments.Development,
				allowManualApproval: false,
				orderHeader: order,
				user: CreateUser("another-user"));

			var result = test.Controller.ConfirmTransferSent(42);

			Assert.IsType<NotFoundResult>(result);
			Assert.Null(order.TransferConfirmedByCustomerAt);
			test.OrderHeaderMock.Verify(x => x.Update(It.IsAny<OrderHeader>()), Times.Never);
			test.UnitOfWorkMock.Verify(x => x.Save(), Times.Never);
		}

		[Fact]
		public void ConfirmTransferSent_AllowsAdminAccessPattern()
		{
			var order = new OrderHeader
			{
				Id = 42,
				ApplicationUserId = "customer-user",
				PaymentMethod = SD.PaymentMethodBankTransfer,
				PaymentStatus = SD.PaymentStatusPending,
				OrderStatus = SD.StatusPending
			};
			var test = CreateController(
				Environments.Development,
				allowManualApproval: false,
				orderHeader: order,
				user: CreateUser("admin-user", SD.Role_Admin));

			var result = test.Controller.ConfirmTransferSent(42);

			Assert.IsType<RedirectToActionResult>(result);
			Assert.NotNull(order.TransferConfirmedByCustomerAt);
			test.OrderHeaderMock.Verify(x => x.Update(order), Times.Once);
			test.UnitOfWorkMock.Verify(x => x.Save(), Times.Once);
		}

		[Fact]
		public void ConfirmTransferSent_DoesNothing_ForNonBankTransferOrder()
		{
			var order = new OrderHeader
			{
				Id = 42,
				ApplicationUserId = "customer-user",
				PaymentMethod = SD.PaymentMethodStripe,
				PaymentStatus = SD.PaymentStatusPending,
				OrderStatus = SD.StatusPending
			};
			var test = CreateController(
				Environments.Development,
				allowManualApproval: false,
				orderHeader: order,
				user: CreateUser("customer-user"));

			var result = test.Controller.ConfirmTransferSent(42);

			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal("Details", redirect.ActionName);
			Assert.Null(order.TransferConfirmedByCustomerAt);
			test.OrderHeaderMock.Verify(x => x.Update(It.IsAny<OrderHeader>()), Times.Never);
			test.UnitOfWorkMock.Verify(x => x.Save(), Times.Never);
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
		public void CancelOrder_ApprovedBankTransfer_CancelsWithoutStripeRefund()
		{
			var order = new OrderHeader
			{
				Id = 42,
				PaymentMethod = SD.PaymentMethodBankTransfer,
				PaymentIntentId = "pi_manual",
				PaymentStatus = SD.PaymentStatusApproved,
				OrderStatus = SD.StatusApproved
			};
			var test = CreateController(Environments.Development, allowManualApproval: false, orderHeader: order, user: CreateUser("admin-user", SD.Role_Admin));

			var result = test.Controller.CancelOrder();

			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal("Details", redirect.ActionName);
			Assert.Equal(42, redirect.RouteValues?["orderId"]);
			test.PaymentRefundMock.Verify(x => x.RefundPaymentIntent(It.IsAny<string>()), Times.Never);
			test.OrderHeaderMock.Verify(x => x.UpdateStatus(42, SD.StatusCancelled, null), Times.Once);
			test.UnitOfWorkMock.Verify(x => x.Save(), Times.Once);
		}

		[Fact]
		public void CancelOrder_ApprovedStripeOrder_RefundsPaymentIntent()
		{
			var order = new OrderHeader
			{
				Id = 42,
				PaymentMethod = SD.PaymentMethodStripe,
				PaymentIntentId = "pi_stripe",
				PaymentStatus = SD.PaymentStatusApproved,
				OrderStatus = SD.StatusApproved
			};
			var test = CreateController(Environments.Development, allowManualApproval: false, orderHeader: order, user: CreateUser("admin-user", SD.Role_Admin));

			var result = test.Controller.CancelOrder();

			Assert.IsType<RedirectToActionResult>(result);
			test.PaymentRefundMock.Verify(x => x.RefundPaymentIntent("pi_stripe"), Times.Once);
			test.OrderHeaderMock.Verify(x => x.UpdateStatus(42, SD.StatusCancelled, SD.StatusRefunded), Times.Once);
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

		[Fact]
		public async Task ShipOrder_DoesNotShipUnpaidCompanyOrder()
		{
			var order = new OrderHeader
			{
				Id = 42,
				CompanyId = 7,
				PaymentStatus = SD.PaymentStatusDelayedPayment,
				OrderStatus = SD.StatusInProcess
			};
			var test = CreateController(
				Environments.Development,
				allowManualApproval: false,
				orderHeader: order,
				user: CreateUser("admin-user", SD.Role_Admin));
			test.Controller.OrderVM.OrderHeader = new OrderHeader
			{
				Id = 42,
				Carrier = "Test Carrier",
				TrackingNumber = "TRACK-1"
			};

			var result = await test.Controller.ShipOrder();

			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal("Details", redirect.ActionName);
			Assert.Equal(42, redirect.RouteValues?["orderId"]);
			Assert.Equal(SD.StatusInProcess, order.OrderStatus);
			test.OrderHeaderMock.Verify(x => x.Update(It.IsAny<OrderHeader>()), Times.Never);
			test.UnitOfWorkMock.Verify(x => x.Save(), Times.Never);
		}

		[Fact]
		public async Task ShipOrder_ShipsPaidProcessingOrder()
		{
			var order = new OrderHeader
			{
				Id = 42,
				PaymentStatus = SD.PaymentStatusApproved,
				OrderStatus = SD.StatusInProcess
			};
			var test = CreateController(
				Environments.Development,
				allowManualApproval: false,
				orderHeader: order,
				user: CreateUser("admin-user", SD.Role_Admin));
			test.Controller.OrderVM.OrderHeader = new OrderHeader
			{
				Id = 42,
				Carrier = "Test Carrier",
				TrackingNumber = "TRACK-1"
			};

			var result = await test.Controller.ShipOrder();

			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal("Details", redirect.ActionName);
			Assert.Equal(42, redirect.RouteValues?["orderId"]);
			Assert.Equal(SD.StatusShipped, order.OrderStatus);
			Assert.Equal("Test Carrier", order.Carrier);
			Assert.Equal("TRACK-1", order.TrackingNumber);
			Assert.NotEqual(default, order.ShippingDate);
			test.OrderHeaderMock.Verify(x => x.Update(order), Times.Once);
			test.UnitOfWorkMock.Verify(x => x.Save(), Times.Once);
		}

		[Fact]
		public void UpdateOrderDetail_DoesNotEditTerminalOrder()
		{
			var order = new OrderHeader
			{
				Id = 42,
				Name = "Original Name",
				PaymentStatus = SD.PaymentStatusPending,
				OrderStatus = SD.StatusCancelled
			};
			var test = CreateController(
				Environments.Development,
				allowManualApproval: false,
				orderHeader: order,
				user: CreateUser("admin-user", SD.Role_Admin));
			test.Controller.OrderVM.OrderHeader = new OrderHeader
			{
				Id = 42,
				Name = "Changed Name",
				Carrier = "Changed Carrier",
				TrackingNumber = "TRACK-1"
			};

			var result = test.Controller.UpdateOrderDetail();

			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal("Details", redirect.ActionName);
			Assert.Equal(42, redirect.RouteValues?["orderId"]);
			Assert.Equal("Original Name", order.Name);
			Assert.Null(order.Carrier);
			Assert.Null(order.TrackingNumber);
			test.OrderHeaderMock.Verify(x => x.Update(It.IsAny<OrderHeader>()), Times.Never);
			test.UnitOfWorkMock.Verify(x => x.Save(), Times.Never);
		}

		[Fact]
		public void GetAll_PendingIncludesCustomerPendingAndCompanyDelayedPaymentOrders()
		{
			var orders = new[]
			{
				new OrderHeader
				{
					Id = 1,
					PaymentStatus = SD.PaymentStatusPending,
					OrderStatus = SD.StatusPending,
					OrderDate = new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc)
				},
				new OrderHeader
				{
					Id = 2,
					CompanyId = 7,
					Company = new Company { Id = 7, Name = "Acme Corp" },
					PaymentStatus = SD.PaymentStatusDelayedPayment,
					OrderStatus = SD.StatusApproved,
					OrderDate = new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc)
				},
				new OrderHeader
				{
					Id = 3,
					PaymentStatus = SD.PaymentStatusApproved,
					OrderStatus = SD.StatusApproved,
					OrderDate = new DateTime(2026, 1, 4, 10, 0, 0, DateTimeKind.Utc)
				}
			};
			var test = CreateController(
				Environments.Development,
				allowManualApproval: false,
				orderHeaders: orders,
				user: CreateUser("admin-user", SD.Role_Admin));

			var result = test.Controller.GetAll("pending");

			var resultOrders = GetJsonOrders(result);
			Assert.Equal(new[] { 2, 1 }, resultOrders.Select(GetJsonOrderId));
			Assert.Equal("Acme Corp", GetNestedJsonString(resultOrders[0], "company", "name"));
		}

		[Fact]
		public void GetAll_SortsNewestOrdersFirst()
		{
			var orders = new[]
			{
				new OrderHeader { Id = 1, OrderDate = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc) },
				new OrderHeader { Id = 2, OrderDate = new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc) },
				new OrderHeader { Id = 4, OrderDate = new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc) },
				new OrderHeader { Id = 3, OrderDate = new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc) }
			};
			var test = CreateController(
				Environments.Development,
				allowManualApproval: false,
				orderHeaders: orders,
				user: CreateUser("admin-user", SD.Role_Admin));

			var result = test.Controller.GetAll("all");

			var resultOrders = GetJsonOrders(result);
			Assert.Equal(new[] { 4, 2, 3, 1 }, resultOrders.Select(GetJsonOrderId));
			test.OrderHeaderMock.Verify(x => x.GetAll(null, "ApplicationUser,Company", false), Times.Once);
		}

		private static TestController CreateController(
			string environmentName,
			bool allowManualApproval,
			OrderHeader? orderHeader = null,
			IEnumerable<OrderHeader>? orderHeaders = null,
			IEnumerable<OrderDetail>? orderDetails = null,
			ApplicationUser? currentUser = null,
			ClaimsPrincipal? user = null)
		{
			var unitOfWorkMock = new Mock<IUnitOfWork>();
			var orderHeaderMock = new Mock<IOrderHeaderRepository>();
			var orderDetailMock = new Mock<IOrderDetailRepository>();
			var applicationUserMock = new Mock<IApplicationUserRepository>();
			var orderHeaderList = (orderHeaders ?? (orderHeader is null ? [] : [orderHeader])).ToList();
			orderHeaderMock
				.Setup(x => x.Get(
					It.IsAny<Expression<Func<OrderHeader, bool>>>(),
					It.IsAny<string?>(),
					It.IsAny<bool>()))
				.Returns((Expression<Func<OrderHeader, bool>> filter, string? _, bool _) =>
					orderHeaderList.SingleOrDefault(filter.Compile()));
			orderHeaderMock
				.Setup(x => x.GetAll(
					It.IsAny<Expression<Func<OrderHeader, bool>>?>(),
					It.IsAny<string?>(),
					It.IsAny<bool>()))
				.Returns((Expression<Func<OrderHeader, bool>>? filter, string? _, bool _) =>
					filter is null
						? orderHeaderList
						: orderHeaderList.Where(filter.Compile()).ToList());

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
			var paymentRefundMock = new Mock<IPaymentRefundService>();
			var emailServiceMock = new Mock<ITransactionalEmailService>();
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
				paymentRefundMock.Object,
				paymentStatusMock.Object,
				environmentMock.Object,
				configuration,
				emailServiceMock.Object)
			{
				OrderVM = new OrderVM { OrderHeader = new OrderHeader { Id = orderHeader?.Id ?? 42 } },
				ControllerContext = new ControllerContext { HttpContext = httpContext },
				TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
			};

			return new TestController(controller, paymentStatusMock, paymentSessionMock, paymentRefundMock, unitOfWorkMock, orderHeaderMock, emailServiceMock);
		}

		private static List<object> GetJsonOrders(IActionResult result)
		{
			var json = Assert.IsType<JsonResult>(result);
			Assert.NotNull(json.Value);
			var dataProperty = json.Value.GetType().GetProperty("data");
			Assert.NotNull(dataProperty);
			var data = Assert.IsAssignableFrom<IEnumerable<object>>(dataProperty!.GetValue(json.Value));
			return data.ToList();
		}

		private static int GetJsonOrderId(object order)
		{
			var idProperty = order.GetType().GetProperty("id");
			Assert.NotNull(idProperty);
			return Assert.IsType<int>(idProperty!.GetValue(order));
		}

		private static string? GetNestedJsonString(object value, string propertyName, string nestedPropertyName)
		{
			var property = value.GetType().GetProperty(propertyName);
			Assert.NotNull(property);
			var nestedValue = property!.GetValue(value);
			Assert.NotNull(nestedValue);
			var nestedProperty = nestedValue!.GetType().GetProperty(nestedPropertyName);
			Assert.NotNull(nestedProperty);
			return nestedProperty!.GetValue(nestedValue) as string;
		}

		private static ClaimsPrincipal CreateUser(string userId, params string[] roles)
		{
			var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
			claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
			return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
		}

		private sealed record TestController(OrderController Controller, Mock<IPaymentStatusService> PaymentStatusMock, Mock<IPaymentSessionService> PaymentSessionMock, Mock<IPaymentRefundService> PaymentRefundMock, Mock<IUnitOfWork> UnitOfWorkMock, Mock<IOrderHeaderRepository> OrderHeaderMock, Mock<ITransactionalEmailService> EmailServiceMock);
	}
}
