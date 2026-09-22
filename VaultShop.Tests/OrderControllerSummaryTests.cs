using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models.ViewModels;
using VaultShop.Utility;
using VaultShop.Web.Areas.Admin.Controllers;
using VaultShop.Web.Services;
using VaultShop.Web.Services.Billing;
using VaultShop.Web.Services.Email;
using VaultShop.Web.Services.Payments;
using VaultShop.Web.Services.Shipping;

namespace VaultShop.Web.Tests
{
	public class OrderControllerSummaryTests
	{
		[Fact]
		public void Summary_AuthorizedUser_ReturnsViewWithModel()
		{
			var (controller, summaryMock, _) = CreateController();
			var summary = new OrderSummaryViewModel { OrderId = 42, CustomerName = "Test", OrderTotal = 100 };
			summaryMock.Setup(x => x.GetSummary(42, controller.User)).Returns(summary);

			var result = controller.Summary(42);

			var view = Assert.IsType<ViewResult>(result);
			Assert.Same(summary, view.Model);
		}

		[Fact]
		public void Summary_ForeignOrder_ReturnsNotFound()
		{
			var (controller, summaryMock, _) = CreateController();
			summaryMock.Setup(x => x.GetSummary(99, controller.User)).Returns((OrderSummaryViewModel?)null);

			var result = controller.Summary(99);

			Assert.IsType<NotFoundResult>(result);
		}

		[Fact]
		public void DownloadSummary_AuthorizedUser_ReturnsPdfFile()
		{
			var (controller, summaryMock, pdfMock) = CreateController();
			var summary = new OrderSummaryViewModel { OrderId = 42, CustomerName = "Test", OrderTotal = 100 };
			summaryMock.Setup(x => x.GetSummary(42, controller.User)).Returns(summary);
			pdfMock.Setup(x => x.Generate(summary)).Returns([0x25, 0x50, 0x44, 0x46, 0x0A]);

			var result = controller.DownloadSummary(42);

			var file = Assert.IsType<FileContentResult>(result);
			Assert.Equal("application/pdf", file.ContentType);
			Assert.Equal("order-42-summary.pdf", file.FileDownloadName);
			Assert.NotEmpty(file.FileContents);
		}

		[Fact]
		public void DownloadSummary_ForeignOrder_ReturnsNotFound()
		{
			var (controller, summaryMock, _) = CreateController();
			summaryMock.Setup(x => x.GetSummary(99, controller.User)).Returns((OrderSummaryViewModel?)null);

			var result = controller.DownloadSummary(99);

			Assert.IsType<NotFoundResult>(result);
		}

		private static (OrderController Controller, Mock<IOrderSummaryService> SummaryMock, Mock<IOrderSummaryPdfGenerator> PdfMock) CreateController()
		{
			var unitOfWorkMock = new Mock<IUnitOfWork>();
			var localizerMock = new Mock<IStringLocalizer<OrderController>>();
			localizerMock.Setup(x => x[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));
			var summaryMock = new Mock<IOrderSummaryService>();
			var pdfMock = new Mock<IOrderSummaryPdfGenerator>();
			var requestServices = new ServiceCollection();
			requestServices.AddKeyedScoped<IPaymentSessionService>(SD.PaymentMethodStripe, (_, _) => Mock.Of<IPaymentSessionService>());
			requestServices.AddKeyedScoped<IPaymentSessionService>(SD.PaymentMethodMercadoPago, (_, _) => Mock.Of<IPaymentSessionService>());
			requestServices.AddKeyedScoped<IPaymentRefundService>(SD.PaymentMethodMercadoPago, (_, _) => Mock.Of<IPaymentRefundService>());

			var httpContext = new DefaultHttpContext
			{
				User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "TestAuth"))
			};

			var controller = new OrderController(
				unitOfWorkMock.Object,
				localizerMock.Object,
				NullLogger<OrderController>.Instance,
				requestServices.BuildServiceProvider(),
				Mock.Of<IPaymentRefundService>(),
				Mock.Of<IPaymentStatusService>(),
				Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(),
				new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
				Mock.Of<ITransactionalEmailService>(),
				summaryMock.Object,
				pdfMock.Object,
				new OrderAccessPolicy(unitOfWorkMock.Object),
				Mock.Of<IBranchLookupService>())
			{
				ControllerContext = new ControllerContext { HttpContext = httpContext },
				TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
			};

			return (controller, summaryMock, pdfMock);
		}
	}
}

