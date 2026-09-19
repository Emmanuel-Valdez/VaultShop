using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Utility;
using VaultShop.Web.Areas.Customer.Controllers;
using VaultShop.Web.Services;
using VaultShop.Web.Services.Checkout;
using VaultShop.Web.Services.Email;
using VaultShop.Web.Services.Payments;
using VaultShop.Web.Services.Pagination;

namespace VaultShop.Web.Tests
{
	public class HomeControllerAddToCartStockTests
	{
		[Fact]
		public void DetailsPost_ExceedsStock_RedirectsWithErrorAndDoesNotAdd()
		{
			var (controller, unitOfWorkMock, cartMock) = CreateController();
			cartMock.Setup(c => c.Get(It.IsAny<Expression<Func<ShoppingCart, bool>>>(), null, false))
				.Returns(new ShoppingCart { ProductId = 1, Count = 2 });

			var result = controller.Details(new ShoppingCart { ProductId = 1, Count = 1 });

			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal(nameof(HomeController.Details), redirect.ActionName);
			Assert.Equal("NotEnoughStock", controller.TempData["error"]);
			cartMock.Verify(c => c.Add(It.IsAny<ShoppingCart>()), Times.Never);
			cartMock.Verify(c => c.Update(It.IsAny<ShoppingCart>()), Times.Never);
			unitOfWorkMock.Verify(u => u.Save(), Times.Never);
		}

		// 6.5 — boundary theory
		[Theory]
		[InlineData(0, 1, 1, true)]
		[InlineData(2, 1, 2, false)]
		[InlineData(0, 1, 0, false)]
		public void DetailsPost_StockBoundaries_Theory(int existingCount, int requestedCount, int stockQuantity, bool shouldSucceed)
		{
			var (controller, unitOfWorkMock, cartMock) = CreateControllerWith(stockQuantity, existingCount);

			var result = controller.Details(new ShoppingCart { ProductId = 1, Count = requestedCount });

			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal(nameof(HomeController.Details), redirect.ActionName);
			if (shouldSucceed)
			{
				Assert.Null(controller.TempData["error"]);
				unitOfWorkMock.Verify(u => u.Save(), Times.Once);
			}
			else
			{
				Assert.Equal("NotEnoughStock", controller.TempData["error"]);
				cartMock.Verify(c => c.Add(It.IsAny<ShoppingCart>()), Times.Never);
				cartMock.Verify(c => c.Update(It.IsAny<ShoppingCart>()), Times.Never);
				unitOfWorkMock.Verify(u => u.Save(), Times.Never);
			}
		}

		// 6.4 — tampered quantity guard
		[Theory]
		[InlineData(0)]
		[InlineData(-5)]
		public void DetailsPost_TamperedQuantity_RejectedAndDoesNotMutate(int tamperedCount)
		{
			var (controller, unitOfWorkMock, cartMock) = CreateControllerWith(stockQuantity: 10, existingCount: 0);

			var result = controller.Details(new ShoppingCart { ProductId = 1, Count = tamperedCount });

			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal(nameof(HomeController.Details), redirect.ActionName);
			Assert.Equal("NotEnoughStock", controller.TempData["error"]);
			cartMock.Verify(c => c.Add(It.IsAny<ShoppingCart>()), Times.Never);
			cartMock.Verify(c => c.Update(It.IsAny<ShoppingCart>()), Times.Never);
			unitOfWorkMock.Verify(u => u.Save(), Times.Never);
		}

		[Fact]
		public void Plus_BelowLimit_Increments()
		{
			var (controller, unitOfWorkMock, cartMock, productMock) = CreateCartController(cartCount: 1, stockQuantity: 5);

			var result = controller.Plus(10);

			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal(nameof(CartController.Index), redirect.ActionName);
			Assert.Null(controller.TempData["error"]);
			cartMock.Verify(c => c.Update(It.Is<ShoppingCart>(s => s.Count == 2)), Times.Once);
			unitOfWorkMock.Verify(u => u.Save(), Times.Once);
			productMock.Verify(p => p.Get(It.IsAny<Expression<Func<Product, bool>>>(), null, false), Times.Once);
		}

		[Fact]
		public void Product_StockQuantity_Negative_FailsValidation()
		{
			var product = new Product
			{
				Id = 1,
				Name = "Test",
				Description = "Desc",
				ListPrice = 1000,
				FinalRetailPrice = 1000,
				FinalWholesalePrice = 800,
				MaxExpectation = 10,
				CategoryId = 1,
				Category = new Category { Id = 1, Name = "Cat", AvgShippingCost = 100m },
				StockQuantity = -1,
				IsAvailableInStore = true,
				IsDeleted = false
			};

			var ctx = new ValidationContext(product);
			var results = new List<ValidationResult>();
			var valid = Validator.TryValidateObject(product, ctx, results, validateAllProperties: true);

			Assert.False(valid);
			Assert.Contains(results, r => r.MemberNames.Contains(nameof(Product.StockQuantity)));
		}

		[Theory]
		[InlineData(0, false)]
		[InlineData(10001, false)]
		[InlineData(1, true)]
		[InlineData(10000, true)]
		public void Product_MaxExpectation_Boundaries_Theory(int expectation, bool shouldBeValid)
		{
			var product = new Product
			{
				Id = 1,
				Name = "Test",
				Description = "Desc",
				ListPrice = 1000,
				FinalRetailPrice = 1000,
				FinalWholesalePrice = 800,
				MaxExpectation = expectation,
				CategoryId = 1,
				Category = new Category { Id = 1, Name = "Cat", AvgShippingCost = 100m },
				StockQuantity = 10,
				IsAvailableInStore = true,
				IsDeleted = false
			};

			var ctx = new ValidationContext(product);
			var results = new List<ValidationResult>();
			var valid = Validator.TryValidateObject(product, ctx, results, validateAllProperties: true);

			Assert.Equal(shouldBeValid, valid);
			if (!shouldBeValid)
			{
				Assert.Contains(results, r => r.MemberNames.Contains(nameof(Product.MaxExpectation)));
			}
		}

		private static (HomeController Controller, Mock<IUnitOfWork> UnitOfWorkMock, Mock<IShoppingCartRepository> CartMock) CreateController()
		{
			return CreateControllerWith(stockQuantity: 2, existingCount: 2);
		}

		private static (HomeController Controller, Mock<IUnitOfWork> UnitOfWorkMock, Mock<IShoppingCartRepository> CartMock) CreateControllerWith(int stockQuantity, int existingCount)
		{
			var unitOfWorkMock = new Mock<IUnitOfWork>();
			var productMock = new Mock<IProductRepository>();
			var cartMock = new Mock<IShoppingCartRepository>();
			unitOfWorkMock.SetupGet(u => u.Product).Returns(productMock.Object);
			unitOfWorkMock.SetupGet(u => u.ShoppingCart).Returns(cartMock.Object);
			productMock.Setup(p => p.Get(It.IsAny<Expression<Func<Product, bool>>>(), null, false))
				.Returns(new Product { Id = 1, StockQuantity = stockQuantity, IsDeleted = false, IsAvailableInStore = true });

			if (existingCount > 0)
				cartMock.Setup(c => c.Get(It.IsAny<Expression<Func<ShoppingCart, bool>>>(), null, false))
					.Returns(new ShoppingCart { ProductId = 1, Count = existingCount });
			else
				cartMock.Setup(c => c.Get(It.IsAny<Expression<Func<ShoppingCart, bool>>>(), null, false))
					.Returns((ShoppingCart?)null);

			var localizerMock = new Mock<IStringLocalizer<HomeController>>();
			localizerMock.Setup(x => x[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));

			var httpContext = new DefaultHttpContext
			{
				User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "TestAuth"))
			};
			httpContext.Session = Mock.Of<ISession>();

			var controller = new HomeController(
				NullLogger<HomeController>.Instance,
				unitOfWorkMock.Object,
				localizerMock.Object,
				Options.Create(new PaginationOptions()))
			{
				ControllerContext = new ControllerContext { HttpContext = httpContext },
				TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
			};

			return (controller, unitOfWorkMock, cartMock);
		}

		private static (CartController Controller, Mock<IUnitOfWork> UnitOfWorkMock, Mock<IShoppingCartRepository> CartMock, Mock<IProductRepository> ProductMock) CreateCartController(int cartCount, int stockQuantity)
		{
			var unitOfWorkMock = new Mock<IUnitOfWork>();
			var productMock = new Mock<IProductRepository>();
			var cartMock = new Mock<IShoppingCartRepository>();
			unitOfWorkMock.SetupGet(u => u.Product).Returns(productMock.Object);
			unitOfWorkMock.SetupGet(u => u.ShoppingCart).Returns(cartMock.Object);

			var cart = new ShoppingCart { Id = 10, ProductId = 1, Count = cartCount, ApplicationUserId = "user-1" };
			cartMock.Setup(c => c.Get(It.IsAny<Expression<Func<ShoppingCart, bool>>>(), null, false))
				.Returns(cart);
			cartMock.Setup(c => c.Get(It.IsAny<Expression<Func<ShoppingCart, bool>>>(), null, true))
				.Returns(cart);

			productMock.Setup(p => p.Get(It.IsAny<Expression<Func<Product, bool>>>(), null, false))
				.Returns(new Product { Id = 1, StockQuantity = stockQuantity, IsDeleted = false, IsAvailableInStore = true });

			unitOfWorkMock.SetupGet(u => u.Company).Returns(Mock.Of<ICompanyRepository>());
			unitOfWorkMock.SetupGet(u => u.ApplicationUser).Returns(Mock.Of<IApplicationUserRepository>());

			var localizerMock = new Mock<IStringLocalizer<CartController>>();
			localizerMock.Setup(x => x[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));

			var httpContext = new DefaultHttpContext
			{
				User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "TestAuth"))
			};
			httpContext.Session = Mock.Of<ISession>();

		var controller = new CartController(
			unitOfWorkMock.Object,
			localizerMock.Object,
			null!,
			NullLogger<CartController>.Instance,
			null!,
			null!,
			null!,
			null!,
			null!,
			null!,
			null!,
			null!)
			{
				ControllerContext = new ControllerContext { HttpContext = httpContext },
				TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
			};

			return (controller, unitOfWorkMock, cartMock, productMock);
		}
	}
}
