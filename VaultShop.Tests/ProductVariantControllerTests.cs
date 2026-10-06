using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Localization;
using Moq;
using VaultShop.Web.Areas.Admin.Controllers;
using VaultShop.Web.Services.ProductVariants;

namespace VaultShop.Web.Tests
{
	public class ProductVariantControllerTests
	{
		[Fact]
		public void Manage_WithData_ReturnsViewWithModel()
		{
			var data = new IProductVariantService.VariantAdminData();
			var (controller, serviceMock, _) = CreateController();
			serviceMock.Setup(s => s.GetAdminData(5)).Returns(data);

			var result = controller.Manage(5);

			var view = Assert.IsType<ViewResult>(result);
			Assert.Same(data, view.Model);
		}

		[Fact]
		public void Manage_UnknownProduct_ReturnsNotFound()
		{
			var (controller, serviceMock, _) = CreateController();
			serviceMock.Setup(s => s.GetAdminData(99)).Returns((IProductVariantService.VariantAdminData?)null);

			Assert.IsType<NotFoundResult>(controller.Manage(99));
		}

		[Fact]
		public void AddValue_Success_RedirectsWithSuccessMessage()
		{
			var (controller, serviceMock, _) = CreateController();
			serviceMock.Setup(s => s.AddValue(5, "Casa", "Gryffindor", 0))
				.Returns(IProductVariantService.VariantResult.Ok());

			var result = controller.AddValue(5, "Casa", "Gryffindor", 0);

			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal(nameof(ProductVariantController.Manage), redirect.ActionName);
			Assert.Equal(5, redirect.RouteValues!["productId"]);
			Assert.Equal("VariantValueAdded", controller.TempData["success"]);
		}

		[Fact]
		public void AddValue_Failure_RedirectsWithErrorKey()
		{
			var (controller, serviceMock, _) = CreateController();
			serviceMock.Setup(s => s.AddValue(5, "Casa", "gryffindor", 0))
				.Returns(IProductVariantService.VariantResult.Fail("VariantValueAlreadyExists"));

			var result = controller.AddValue(5, "Casa", "gryffindor", 0);

			Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal("VariantValueAlreadyExists", controller.TempData["error"]);
		}

		[Fact]
		public void DeleteValue_CrossProduct_RejectedWithoutSuccessMessage()
		{
			var (controller, serviceMock, _) = CreateController();
			serviceMock.Setup(s => s.DeleteValue(5, 77))
				.Returns(IProductVariantService.VariantResult.Fail("VariantInvalid"));

			var result = controller.DeleteValue(5, 77);

			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal(5, redirect.RouteValues!["productId"]);
			Assert.Equal("VariantInvalid", controller.TempData["error"]);
			Assert.Null(controller.TempData["success"]);
		}

		[Fact]
		public void Generate_Success_ReportsCreatedCount()
		{
			var (controller, serviceMock, localizerMock) = CreateController();
			serviceMock.Setup(s => s.GenerateCombinations(5))
				.Returns(IProductVariantService.VariantResult.Ok(8));

			var result = controller.Generate(5);

			Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal("CombinationsGenerated", controller.TempData["success"]);
			localizerMock.Verify(x => x["CombinationsGenerated", It.Is<object[]>(a => a != null && a.Length == 1 && (int)a[0] == 8)], Times.Once);
		}

		[Fact]
		public void SetAvailability_Failure_RedirectsWithErrorKey()
		{
			var (controller, serviceMock, _) = CreateController();
			serviceMock.Setup(s => s.SetAvailability(5, 9, false))
				.Returns(IProductVariantService.VariantResult.Fail("VariantNotFound"));

			var result = controller.SetAvailability(5, 9, false);

			Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal("VariantNotFound", controller.TempData["error"]);
		}

		[Fact]
		public void DeleteVariant_BlockedByCart_RedirectsWithErrorKey()
		{
			var (controller, serviceMock, _) = CreateController();
			serviceMock.Setup(s => s.DeleteVariant(5, 9))
				.Returns(IProductVariantService.VariantResult.Fail("VariantReferencedByCart"));

			var result = controller.DeleteVariant(5, 9);

			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal(nameof(ProductVariantController.Manage), redirect.ActionName);
			Assert.Equal("VariantReferencedByCart", controller.TempData["error"]);
		}

		[Fact]
		public void DeleteVariant_Success_RedirectsWithSuccessMessage()
		{
			var (controller, serviceMock, _) = CreateController();
			serviceMock.Setup(s => s.DeleteVariant(5, 9))
				.Returns(IProductVariantService.VariantResult.Ok());

			var result = controller.DeleteVariant(5, 9);

			Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal("VariantDeleted", controller.TempData["success"]);
		}

		private static (ProductVariantController Controller, Mock<IProductVariantService> ServiceMock, Mock<IStringLocalizer<ProductVariantController>> LocalizerMock) CreateController()
		{
			var serviceMock = new Mock<IProductVariantService>();
			var localizerMock = new Mock<IStringLocalizer<ProductVariantController>>();
			localizerMock.Setup(x => x[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));
			localizerMock.Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()])
				.Returns((string name, object[] args) => new LocalizedString(name, name));

			var httpContext = new DefaultHttpContext();
			httpContext.Session = Mock.Of<ISession>();

			var controller = new ProductVariantController(serviceMock.Object, localizerMock.Object)
			{
				ControllerContext = new ControllerContext { HttpContext = httpContext },
				TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
			};

			return (controller, serviceMock, localizerMock);
		}
	}
}
