using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Utility;
using VaultShop.Web.Services.Checkout;
using VaultShop.Web.Services.Pricing;
using VaultShop.Web.Services.ProductVariants;

namespace VaultShop.Web.Tests
{
	public class CheckoutServiceTests
	{
		[Fact]
		public void CreateOrder_EmptyCart_ReturnsCartEmptyAndDoesNotCreateOrder()
		{
			var unitOfWork = CreateUnitOfWork([], [CreateUser("user-1")]);
			var service = CreateService(unitOfWork.Mock.Object);

			var result = service.CreateOrder("user-1", new OrderHeader(), useWholesalePrice: false);

			Assert.True(result.IsCartEmpty);
			unitOfWork.OrderHeaderMock.Verify(x => x.Add(It.IsAny<OrderHeader>()), Times.Never);
			unitOfWork.OrderDetailMock.Verify(x => x.Add(It.IsAny<OrderDetail>()), Times.Never);
			unitOfWork.Mock.Verify(x => x.Save(), Times.Never);
		}

		[Fact]
		public void CreateOrder_ValidCustomerCart_CreatesOrderWithPendingPayment()
		{
			var carts = new[]
			{
				CreateCart("user-1", productId: 10, count: 1, retailPrice: 100m, wholesalePrice: 70m)
			};
			var unitOfWork = CreateUnitOfWork(carts, [CreateUser("user-1")]);
			var service = CreateService(unitOfWork.Mock.Object);

			var result = service.CreateOrder("user-1", new OrderHeader { PaymentMethod = SD.PaymentMethodStripe }, useWholesalePrice: false);

			Assert.False(result.IsCartEmpty);
			Assert.True(result.RequiresOnlinePayment);
			Assert.Equal(123, result.OrderId);
			var orderHeader = Assert.Single(unitOfWork.AddedOrderHeaders);
			Assert.Equal("user-1", orderHeader.ApplicationUserId);
			Assert.Equal(SD.PaymentStatusPending, orderHeader.PaymentStatus);
			Assert.Equal(SD.StatusPending, orderHeader.OrderStatus);
			var orderDetail = Assert.Single(unitOfWork.AddedOrderDetails);
			Assert.Equal(10, orderDetail.ProductId);
			Assert.Equal(1, orderDetail.Count);
			Assert.Equal(100m, orderDetail.Price);
			unitOfWork.Mock.Verify(x => x.Save(), Times.Exactly(2));
		}

		[Fact]
		public void CreateOrder_ValidCart_CalculatesRetailTotalCorrectly()
		{
			var carts = new[]
			{
				CreateCart("user-1", productId: 10, count: 2, retailPrice: 100m, wholesalePrice: 70m),
				CreateCart("user-1", productId: 11, count: 1, retailPrice: 50m, wholesalePrice: 35m)
			};
			var unitOfWork = CreateUnitOfWork(carts, [CreateUser("user-1")]);
			var service = CreateService(unitOfWork.Mock.Object);

			var result = service.CreateOrder("user-1", new OrderHeader(), useWholesalePrice: false);

			Assert.False(result.OrderTotalInvalid);
			var orderHeader = Assert.Single(unitOfWork.AddedOrderHeaders);
			Assert.Equal(250m, orderHeader.OrderTotal);
			Assert.Collection(unitOfWork.AddedOrderDetails,
				first => Assert.Equal(100m, first.Price),
				second => Assert.Equal(50m, second.Price));
		}

		[Fact]
		public void CreateOrder_ValidCompanyUser_CreatesPendingDelayedPaymentOrder()
		{
			var carts = new[]
			{
				CreateCart("user-1", productId: 10, count: 1, retailPrice: 100m, wholesalePrice: 70m)
			};
			var unitOfWork = CreateUnitOfWork(
				carts,
				[CreateUser("user-1", companyId: 7)],
				[CreateCompany(7)]);
			var service = CreateService(unitOfWork.Mock.Object);

			var result = service.CreateOrder("user-1", new OrderHeader { PaymentMethod = SD.PaymentMethodBankTransfer }, useWholesalePrice: false);

			Assert.False(result.RequiresOnlinePayment);
			var orderHeader = Assert.Single(unitOfWork.AddedOrderHeaders);
			Assert.Equal(7, orderHeader.CompanyId);
			Assert.Equal(SD.PaymentStatusDelayedPayment, orderHeader.PaymentStatus);
			Assert.Equal(SD.StatusPending, orderHeader.OrderStatus);
			Assert.Null(orderHeader.PaymentMethod);
			Assert.Equal(DateOnly.FromDateTime(orderHeader.OrderDate.AddDays(SD.CompanyPaymentDueDays)), orderHeader.PaymentDueDate);
		}

		[Fact]
		public void CreateOrder_CompanyUser_SnapshotsFiscalFields()
		{
			var carts = new[]
			{
				CreateCart("user-1", productId: 10, count: 1, retailPrice: 100m, wholesalePrice: 70m)
			};
			var company = new Company
			{
				Id = 7,
				Name = "Textiles SA",
				RazonSocial = "Textiles SA SRL",
				DomicilioFiscal = "Av. Corrientes 1234, CABA",
				Cuit = "30-71234567-9"
			};
			var unitOfWork = CreateUnitOfWork(carts, [CreateUser("user-1", companyId: 7)], [company]);
			var service = CreateService(unitOfWork.Mock.Object);

			var result = service.CreateOrder("user-1", new OrderHeader { PaymentMethod = SD.PaymentMethodBankTransfer }, useWholesalePrice: false);

			var orderHeader = Assert.Single(unitOfWork.AddedOrderHeaders);
			Assert.Equal("Textiles SA SRL", orderHeader.RazonSocialSnapshot);
			Assert.Equal("Av. Corrientes 1234, CABA", orderHeader.DomicilioFiscalSnapshot);
			Assert.Equal("30-71234567-9", orderHeader.CuitSnapshot);
		}

		[Fact]
		public void CreateOrder_ValidCart_DecrementsStockPerLine()
		{
			var carts = new[]
			{
				CreateCart("user-1", productId: 10, count: 2, retailPrice: 100m, wholesalePrice: 70m, stockQuantity: 5),
				CreateCart("user-1", productId: 11, count: 1, retailPrice: 50m, wholesalePrice: 35m, stockQuantity: 3)
			};
			var unitOfWork = CreateUnitOfWork(carts, [CreateUser("user-1")]);
			var service = CreateService(unitOfWork.Mock.Object);

			var result = service.CreateOrder("user-1", new OrderHeader(), useWholesalePrice: false);

		Assert.False(result.InsufficientStock);
		Assert.Equal(3, carts[0].Product.StockQuantity);
		Assert.Equal(2, carts[1].Product.StockQuantity);
		unitOfWork.ProductMock.Verify(x => x.DecrementStockIfSufficient(It.IsAny<int>(), It.IsAny<int>()), Times.Exactly(2));
	}

	[Fact]
	public void CreateOrder_InsufficientStock_ReturnsFlagAndNoSideEffects()
		{
			var carts = new[]
			{
				CreateCart("user-1", productId: 10, count: 2, retailPrice: 100m, wholesalePrice: 70m, stockQuantity: 5),
				CreateCart("user-1", productId: 11, count: 2, retailPrice: 50m, wholesalePrice: 35m, stockQuantity: 1)
			};
			var unitOfWork = CreateUnitOfWork(carts, [CreateUser("user-1")]);
			var service = CreateService(unitOfWork.Mock.Object);

			var result = service.CreateOrder("user-1", new OrderHeader(), useWholesalePrice: false);

			Assert.True(result.InsufficientStock);
			Assert.Null(result.OrderId);
		// First line was valid but nothing may persist: no order, no details, no saves, no decrement.
		unitOfWork.OrderHeaderMock.Verify(x => x.Add(It.IsAny<OrderHeader>()), Times.Never);
		unitOfWork.OrderDetailMock.Verify(x => x.Add(It.IsAny<OrderDetail>()), Times.Never);
		unitOfWork.Mock.Verify(x => x.Save(), Times.Never);
		unitOfWork.ProductMock.Verify(x => x.DecrementStockIfSufficient(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
			Assert.Equal(5, carts[0].Product.StockQuantity);
			Assert.Equal(1, carts[1].Product.StockQuantity);
		}

		[Fact]
		public void CreateOrder_ValidCompanyUser_DecrementsStock()
		{
			var carts = new[]
			{
				CreateCart("user-1", productId: 10, count: 2, retailPrice: 100m, wholesalePrice: 70m, stockQuantity: 5)
			};
			var unitOfWork = CreateUnitOfWork(
				carts,
				[CreateUser("user-1", companyId: 7)],
				[CreateCompany(7)]);
			var service = CreateService(unitOfWork.Mock.Object);

			var result = service.CreateOrder("user-1", new OrderHeader { PaymentMethod = SD.PaymentMethodBankTransfer }, useWholesalePrice: false);

		Assert.False(result.InsufficientStock);
		Assert.Equal(3, carts[0].Product.StockQuantity);
		unitOfWork.ProductMock.Verify(x => x.DecrementStockIfSufficient(10, 2), Times.Once);
		}

	[Fact]
	public void CreateOrder_ConditionalWriteReportsZero_ReturnsInsufficientStock()
	{
		// product-variants-hardening 1.2: pre-read passes (2 ≤ 5) but the pool was consumed
		// concurrently, so the conditional write affects 0 rows. Fails if checkout ever
		// reverts to an absolute write of the pre-read value.
		var carts = new[]
		{
			CreateCart("user-1", productId: 10, count: 2, retailPrice: 100m, wholesalePrice: 70m, stockQuantity: 5)
		};
		var unitOfWork = CreateUnitOfWork(carts, [CreateUser("user-1")]);
		unitOfWork.ProductMock
			.Setup(x => x.DecrementStockIfSufficient(It.IsAny<int>(), It.IsAny<int>()))
			.Returns(0);
		var service = CreateService(unitOfWork.Mock.Object);

		var result = service.CreateOrder("user-1", new OrderHeader(), useWholesalePrice: false);

		Assert.True(result.InsufficientStock);
		Assert.Null(result.OrderId);
		unitOfWork.OrderHeaderMock.Verify(x => x.Add(It.IsAny<OrderHeader>()), Times.Never);
		unitOfWork.OrderDetailMock.Verify(x => x.Add(It.IsAny<OrderDetail>()), Times.Never);
	}

	[Fact]
	public void CreateOrder_OrderDetailCreationFails_RethrowsException()
		{
			var carts = new[]
			{
				CreateCart("user-1", productId: 10, count: 1, retailPrice: 100m, wholesalePrice: 70m)
			};
			var unitOfWork = CreateUnitOfWork(carts, [CreateUser("user-1")], throwWhenAddingOrderDetail: true);
			var service = CreateService(unitOfWork.Mock.Object);

			Assert.Throws<InvalidOperationException>(() => service.CreateOrder("user-1", new OrderHeader(), useWholesalePrice: false));
		}

		private static CheckoutService CreateService(IUnitOfWork unitOfWork)
		{
			return new CheckoutService(unitOfWork, NullLogger<CheckoutService>.Instance, Mock.Of<IProductVariantService>(), new DiscountEvaluator());
		}

		private static TestUnitOfWork CreateUnitOfWork(
			IEnumerable<ShoppingCart> shoppingCarts,
			IEnumerable<ApplicationUser> users,
			IEnumerable<Company>? companies = null,
			bool throwWhenAddingOrderDetail = false,
			IEnumerable<Coupon>? coupons = null,
			IEnumerable<Promotion>? promotions = null)
		{
			var testUnitOfWork = new TestUnitOfWork();
			var shoppingCartList = shoppingCarts.ToList();
			var userList = users.ToList();
			var companyList = (companies ?? []).ToList();
			var couponList = (coupons ?? []).ToList();
			var promotionList = (promotions ?? []).ToList();
			var productList = shoppingCartList.Select(cart => cart.Product).ToList();

			testUnitOfWork.ShoppingCartMock
				.Setup(x => x.GetAll(
					It.IsAny<Expression<Func<ShoppingCart, bool>>>(),
					It.IsAny<string?>(),
					It.IsAny<bool>()))
				.Returns((Expression<Func<ShoppingCart, bool>> filter, string? _, bool _) => shoppingCartList.Where(filter.Compile()).ToList());

			testUnitOfWork.ApplicationUserMock
				.Setup(x => x.Get(
					It.IsAny<Expression<Func<ApplicationUser, bool>>>(),
					It.IsAny<string?>(),
					It.IsAny<bool>()))
				.Returns((Expression<Func<ApplicationUser, bool>> filter, string? _, bool _) => userList.SingleOrDefault(filter.Compile()));

			testUnitOfWork.CompanyMock
				.Setup(x => x.Get(
					It.IsAny<Expression<Func<Company, bool>>>(),
					It.IsAny<string?>(),
					It.IsAny<bool>()))
				.Returns((Expression<Func<Company, bool>> filter, string? _, bool _) => companyList.SingleOrDefault(filter.Compile()));

		testUnitOfWork.ProductMock
			.Setup(x => x.Get(
				It.IsAny<Expression<Func<Product, bool>>>(),
				It.IsAny<string?>(),
				It.IsAny<bool>()))
			.Returns((Expression<Func<Product, bool>> filter, string? _, bool _) => productList.SingleOrDefault(filter.Compile()));

		// product-variants-hardening 1.1: mirror the conditional-write semantics on the in-memory list.
		testUnitOfWork.ProductMock
			.Setup(x => x.DecrementStockIfSufficient(It.IsAny<int>(), It.IsAny<int>()))
			.Returns((int productId, int total) =>
			{
				var product = productList.SingleOrDefault(p => p.Id == productId);
				if (product == null || product.StockQuantity < total)
				{
					return 0;
				}
				product.StockQuantity -= total;
				return 1;
			});

			testUnitOfWork.OrderHeaderMock
				.Setup(x => x.Add(It.IsAny<OrderHeader>()))
				.Callback<OrderHeader>(orderHeader =>
				{
					orderHeader.Id = 123;
					testUnitOfWork.AddedOrderHeaders.Add(orderHeader);
				});

			testUnitOfWork.OrderDetailMock
				.Setup(x => x.Add(It.IsAny<OrderDetail>()))
				.Callback<OrderDetail>(orderDetail =>
				{
					if (throwWhenAddingOrderDetail)
					{
						throw new InvalidOperationException("Simulated order detail persistence failure.");
					}

					testUnitOfWork.AddedOrderDetails.Add(orderDetail);
				});

			testUnitOfWork.Mock
				.Setup(x => x.ExecuteInTransaction(It.IsAny<Action>()))
				.Callback<Action>(operation => operation());

			testUnitOfWork.Mock.Setup(x => x.ShoppingCart).Returns(testUnitOfWork.ShoppingCartMock.Object);
			testUnitOfWork.Mock.Setup(x => x.ApplicationUser).Returns(testUnitOfWork.ApplicationUserMock.Object);
			testUnitOfWork.Mock.Setup(x => x.Company).Returns(testUnitOfWork.CompanyMock.Object);
			testUnitOfWork.Mock.Setup(x => x.OrderHeader).Returns(testUnitOfWork.OrderHeaderMock.Object);
			testUnitOfWork.Mock.Setup(x => x.OrderDetail).Returns(testUnitOfWork.OrderDetailMock.Object);
			testUnitOfWork.Mock.Setup(x => x.Product).Returns(testUnitOfWork.ProductMock.Object);

			testUnitOfWork.CouponMock
				.Setup(x => x.GetAll(
					It.IsAny<Expression<Func<Coupon, bool>>>(),
					It.IsAny<string?>(),
					It.IsAny<bool>()))
				.Returns((Expression<Func<Coupon, bool>>? filter, string? _, bool _) =>
					filter is null ? couponList : couponList.Where(filter.Compile()).ToList());
			testUnitOfWork.CouponMock
				.Setup(x => x.Get(
					It.IsAny<Expression<Func<Coupon, bool>>>(),
					It.IsAny<string?>(),
					It.IsAny<bool>()))
				.Returns((Expression<Func<Coupon, bool>> filter, string? _, bool _) => couponList.SingleOrDefault(filter.Compile()));
			testUnitOfWork.PromotionMock
				.Setup(x => x.GetAll(
					It.IsAny<Expression<Func<Promotion, bool>>>(),
					It.IsAny<string?>(),
					It.IsAny<bool>()))
				.Returns((Expression<Func<Promotion, bool>>? filter, string? _, bool _) =>
					filter is null ? promotionList : promotionList.Where(filter.Compile()).ToList());
			testUnitOfWork.Mock.Setup(x => x.Coupon).Returns(testUnitOfWork.CouponMock.Object);
			testUnitOfWork.Mock.Setup(x => x.Promotion).Returns(testUnitOfWork.PromotionMock.Object);

			return testUnitOfWork;
		}

		private static ShoppingCart CreateCart(string userId, int productId, int count, decimal retailPrice, decimal wholesalePrice, int stockQuantity = 100)
		{
			return new ShoppingCart
			{
				Id = productId,
				ApplicationUserId = userId,
				ProductId = productId,
				Count = count,
				Product = new Product
				{
					Id = productId,
					IsDeleted = false,
					IsAvailableInStore = true,
					FinalRetailPrice = retailPrice,
					FinalWholesalePrice = wholesalePrice,
					StockQuantity = stockQuantity
				}
			};
		}

		private static ApplicationUser CreateUser(string userId, int? companyId = null)
		{
			return new ApplicationUser
			{
				Id = userId,
				Name = "Test User",
				CompanyId = companyId
			};
		}

		private static Company CreateCompany(int companyId)
		{
			return new Company
			{
				Id = companyId,
				Name = "Test Company",
				IsDeleted = false
			};
		}

		private sealed class TestUnitOfWork
		{
			public Mock<IUnitOfWork> Mock { get; } = new();
			public Mock<IShoppingCartRepository> ShoppingCartMock { get; } = new();
			public Mock<IApplicationUserRepository> ApplicationUserMock { get; } = new();
			public Mock<ICompanyRepository> CompanyMock { get; } = new();
			public Mock<IProductRepository> ProductMock { get; } = new();
			public Mock<IOrderHeaderRepository> OrderHeaderMock { get; } = new();
			public Mock<IOrderDetailRepository> OrderDetailMock { get; } = new();
			public Mock<ICouponRepository> CouponMock { get; } = new();
			public Mock<IPromotionRepository> PromotionMock { get; } = new();
			public List<OrderHeader> AddedOrderHeaders { get; } = [];
			public List<OrderDetail> AddedOrderDetails { get; } = [];
		}
	}
}
