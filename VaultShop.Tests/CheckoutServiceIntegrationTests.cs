using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository;
using VaultShop.Models;
using VaultShop.Utility;
using VaultShop.Web.Services.Checkout;

namespace VaultShop.Web.Tests
{
	public class CheckoutServiceIntegrationTests
	{
		[Fact]
		public void CreateOrder_WithValidCart_PersistsOrderHeaderAndDetails()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			SeedCheckoutCart(options, "user-1", productId: 10, count: 2);

			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);

				var result = service.CreateOrder("user-1", CreatePostedOrderHeader(), useWholesalePrice: false);

				Assert.False(result.IsCartEmpty);
				Assert.True(result.RequiresOnlinePayment);
				Assert.True(result.OrderId > 0);
			}

			using var verificationContext = new ApplicationDbContext(options);
			var orderHeader = Assert.Single(verificationContext.OrderHeaders.AsNoTracking());
			var orderDetail = Assert.Single(verificationContext.OrderDetails.AsNoTracking());

			Assert.Equal("user-1", orderHeader.ApplicationUserId);
			Assert.Equal(200m, orderHeader.OrderTotal);
			Assert.Equal(SD.PaymentStatusPending, orderHeader.PaymentStatus);
			Assert.Equal(SD.StatusPending, orderHeader.OrderStatus);
			Assert.Equal(orderHeader.Id, orderDetail.OrderHeaderId);
			Assert.Equal(10, orderDetail.ProductId);
			Assert.Equal(2, orderDetail.Count);
			Assert.Equal(100m, orderDetail.Price);
		}

		[Fact]
		public void CreateOrder_WhenOrderDetailSaveFails_RollsBackOrderHeaderAndDetails()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			SeedCheckoutCart(options, "user-1", productId: 10, count: 1);

			using (var context = new FailingSecondSaveChangesDbContext(options))
			{
				var service = CreateService(context);

				Assert.Throws<InvalidOperationException>(() => service.CreateOrder("user-1", CreatePostedOrderHeader(), useWholesalePrice: false));
			}

			using var verificationContext = new ApplicationDbContext(options);
			Assert.Empty(verificationContext.OrderHeaders.AsNoTracking());
			Assert.Empty(verificationContext.OrderDetails.AsNoTracking());
		}

		private static CheckoutService CreateService(ApplicationDbContext context)
		{
			return new CheckoutService(new UnitOfWork(context), NullLogger<CheckoutService>.Instance);
		}

		private static SqliteConnection CreateOpenConnection()
		{
			var connection = new SqliteConnection("Data Source=:memory:");
			connection.Open();
			return connection;
		}

		private static DbContextOptions<ApplicationDbContext> CreateOptions(SqliteConnection connection)
		{
			return new DbContextOptionsBuilder<ApplicationDbContext>()
				.UseSqlite(connection)
				.Options;
		}

		private static void EnsureDatabaseCreated(DbContextOptions<ApplicationDbContext> options)
		{
			using var context = new ApplicationDbContext(options);
			context.Database.EnsureCreated();
		}

		private static void SeedCheckoutCart(DbContextOptions<ApplicationDbContext> options, string userId, int productId, int count)
		{
			using var context = new ApplicationDbContext(options);
			var category = new Category
			{
				Name = "Test Category",
				MaxExpectation = 10,
				AvgShippingCost = 100m,
			};

			var product = new Product
			{
				Id = productId,
				Name = "Test Product",
				Description = "Product used by checkout integration tests.",
				Category = category,
				ListPrice = 100m,
				FinalRetailPrice = 100m,
				FinalWholesalePrice = 70m,
				IsAvailableInStore = true,
				IsDeleted = false,
			};

			var user = new ApplicationUser
			{
				Id = userId,
				UserName = "test@example.com",
				Email = "test@example.com",
				Name = "Test User",
			};

			context.Categories.Add(category);
			context.Products.Add(product);
			context.ApplicationUsers.Add(user);
			context.ShoppingCarts.Add(new ShoppingCart
			{
				ApplicationUserId = userId,
				ProductId = productId,
				Count = count,
			});
			context.SaveChanges();
		}

		private static OrderHeader CreatePostedOrderHeader()
		{
			return new OrderHeader
			{
				Name = "Test User",
				StreetAddress = "123 Test St",
				City = "Buenos Aires",
				State = "Buenos Aires",
				PostalCode = "1000",
				PhoneNumber = "555-0100",
				PaymentMethod = SD.PaymentMethodStripe,
			};
		}

		private sealed class FailingSecondSaveChangesDbContext : ApplicationDbContext
		{
			private int _saveChangesCallCount;

			public FailingSecondSaveChangesDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
			{
			}

			public override int SaveChanges()
			{
				_saveChangesCallCount++;
				if (_saveChangesCallCount == 2)
				{
					throw new InvalidOperationException("Simulated failure while saving order details.");
				}

				return base.SaveChanges();
			}
		}
	}
}
