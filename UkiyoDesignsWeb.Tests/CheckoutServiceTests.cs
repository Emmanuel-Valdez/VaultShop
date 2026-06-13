using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models;
using UkiyoDesigns.Utility;
using UkiyoDesignsWeb.Services.Checkout;

namespace UkiyoDesignsWeb.Tests
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

			var result = service.CreateOrder("user-1", new OrderHeader(), useWholesalePrice: false);

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
		public void CreateOrder_ValidCompanyUser_CreatesApprovedDelayedPaymentOrder()
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

			var result = service.CreateOrder("user-1", new OrderHeader(), useWholesalePrice: false);

			Assert.False(result.RequiresOnlinePayment);
			var orderHeader = Assert.Single(unitOfWork.AddedOrderHeaders);
			Assert.Equal(7, orderHeader.CompanyId);
			Assert.Equal(SD.PaymentStatusDelayedPayment, orderHeader.PaymentStatus);
			Assert.Equal(SD.StatusApproved, orderHeader.OrderStatus);
		}

		private static CheckoutService CreateService(IUnitOfWork unitOfWork)
		{
			return new CheckoutService(unitOfWork, NullLogger<CheckoutService>.Instance);
		}

		private static TestUnitOfWork CreateUnitOfWork(
			IEnumerable<ShoppingCart> shoppingCarts,
			IEnumerable<ApplicationUser> users,
			IEnumerable<Company>? companies = null)
		{
			var testUnitOfWork = new TestUnitOfWork();
			var shoppingCartList = shoppingCarts.ToList();
			var userList = users.ToList();
			var companyList = (companies ?? []).ToList();

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

			testUnitOfWork.OrderHeaderMock
				.Setup(x => x.Add(It.IsAny<OrderHeader>()))
				.Callback<OrderHeader>(orderHeader =>
				{
					orderHeader.Id = 123;
					testUnitOfWork.AddedOrderHeaders.Add(orderHeader);
				});

			testUnitOfWork.OrderDetailMock
				.Setup(x => x.Add(It.IsAny<OrderDetail>()))
				.Callback<OrderDetail>(testUnitOfWork.AddedOrderDetails.Add);

			testUnitOfWork.Mock.Setup(x => x.ShoppingCart).Returns(testUnitOfWork.ShoppingCartMock.Object);
			testUnitOfWork.Mock.Setup(x => x.ApplicationUser).Returns(testUnitOfWork.ApplicationUserMock.Object);
			testUnitOfWork.Mock.Setup(x => x.Company).Returns(testUnitOfWork.CompanyMock.Object);
			testUnitOfWork.Mock.Setup(x => x.OrderHeader).Returns(testUnitOfWork.OrderHeaderMock.Object);
			testUnitOfWork.Mock.Setup(x => x.OrderDetail).Returns(testUnitOfWork.OrderDetailMock.Object);

			return testUnitOfWork;
		}

		private static ShoppingCart CreateCart(string userId, int productId, int count, decimal retailPrice, decimal wholesalePrice)
		{
			return new ShoppingCart
			{
				ApplicationUserId = userId,
				ProductId = productId,
				Count = count,
				Product = new Product
				{
					Id = productId,
					IsDeleted = false,
					IsAvailableInStore = true,
					FinalRetailPrice = retailPrice,
					FinalWholesalePrice = wholesalePrice
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
			public Mock<IOrderHeaderRepository> OrderHeaderMock { get; } = new();
			public Mock<IOrderDetailRepository> OrderDetailMock { get; } = new();
			public List<OrderHeader> AddedOrderHeaders { get; } = [];
			public List<OrderDetail> AddedOrderDetails { get; } = [];
		}
	}
}
