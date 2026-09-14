using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository;
using VaultShop.Models;
using VaultShop.Utility;
using VaultShop.Web.Services;
using VaultShop.Web.Services.Billing;
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
			Assert.Equal(8, verificationContext.Products.AsNoTracking().Single(p => p.Id == 10).StockQuantity);
		}

		[Fact]
		public void CreateOrder_WithInsufficientStock_ReturnsFlagAndPersistsNothing()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			SeedCheckoutCart(options, "user-1", productId: 10, count: 20, stockQuantity: 5);

			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);

				var result = service.CreateOrder("user-1", CreatePostedOrderHeader(), useWholesalePrice: false);

				Assert.True(result.InsufficientStock);
				Assert.Null(result.OrderId);
			}

			using var verificationContext = new ApplicationDbContext(options);
			Assert.Empty(verificationContext.OrderHeaders.AsNoTracking());
			Assert.Empty(verificationContext.OrderDetails.AsNoTracking());
			Assert.Equal(5, verificationContext.Products.AsNoTracking().Single(p => p.Id == 10).StockQuantity);
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

		[Fact]
		public void CreateOrder_SnapshotImmutableAfterCompanyEdit()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			SeedCompanyOrder(options, "user-1", companyId: 7);

			int orderId;
			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				var result = service.CreateOrder("user-1", CreatePostedCompanyOrderHeader(), useWholesalePrice: false);
				orderId = result.OrderId!.Value;
				Assert.True(orderId > 0);
			}

			using (var editContext = new ApplicationDbContext(options))
			{
				var company = editContext.Companies.Single(c => c.Id == 7);
				company.RazonSocial = "RAZON CAMBIADA SA";
				editContext.SaveChanges();
			}

			using var verifyContext = new ApplicationDbContext(options);
			var order = verifyContext.OrderHeaders.Single();
			Assert.Equal("Textiles SA SRL", order.RazonSocialSnapshot);
			Assert.Equal("Av. Corrientes 1234, CABA", order.DomicilioFiscalSnapshot);
			Assert.Equal(8, verifyContext.Products.AsNoTracking().Single(p => p.Id == 10).StockQuantity);

			var summaryService = new OrderSummaryService(new UnitOfWork(verifyContext), new OrderAccessPolicy(new UnitOfWork(verifyContext)));
			var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")]));
			var summary = summaryService.GetSummary(orderId, principal);
			Assert.NotNull(summary);
			Assert.Equal("Textiles SA SRL", summary.RazonSocial);
		}

		[Fact]
		public void CreateOrder_SequentialConcurrentCheckouts_DoNotDecrementStockBelowZero()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			SeedTwoUserSharedStock(options, "user-1", "user-2", productId: 10, count: 2, stockQuantity: 3);

			// First checkout (2 of 3) succeeds, leaving stock at 1.
			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				var result = service.CreateOrder("user-1", CreatePostedOrderHeader(), useWholesalePrice: false);
				Assert.False(result.InsufficientStock);
				Assert.True(result.OrderId > 0);
			}

			// Second checkout (needs 2, only 1 left) is rejected; stock stays 1, no order persisted.
			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				var result = service.CreateOrder("user-2", CreatePostedOrderHeader(), useWholesalePrice: false);
				Assert.True(result.InsufficientStock);
				Assert.Null(result.OrderId);
			}

			using var verifyContext = new ApplicationDbContext(options);
			var orderHeader = Assert.Single(verifyContext.OrderHeaders.AsNoTracking());
			// Only the successful checkout persisted its detail; the rejected one left nothing behind.
			Assert.Single(verifyContext.OrderDetails.AsNoTracking().Where(d => d.OrderHeaderId == orderHeader.Id));
			Assert.Equal(1, verifyContext.Products.AsNoTracking().Single(p => p.Id == 10).StockQuantity);
		}

		[Fact]
		public void CreateOrder_NonCompanyOrder_FiscalSnapshotFieldsAreNull()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			SeedCheckoutCart(options, "user-1", productId: 10, count: 2);

			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);

				var postedHeader = CreatePostedOrderHeader();
				postedHeader.RazonSocialSnapshot = "HACKED Razon";
				postedHeader.DomicilioFiscalSnapshot = "HACKED Domicilio";
				postedHeader.CuitSnapshot = "99-99999999-9";

				var result = service.CreateOrder("user-1", postedHeader, useWholesalePrice: false);

				Assert.True(result.OrderId > 0);
			}

			using var verifyContext = new ApplicationDbContext(options);
			var order = verifyContext.OrderHeaders.AsNoTracking().Single();
			Assert.Null(order.RazonSocialSnapshot);
			Assert.Null(order.DomicilioFiscalSnapshot);
			Assert.Null(order.CuitSnapshot);
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

		private static void SeedCheckoutCart(DbContextOptions<ApplicationDbContext> options, string userId, int productId, int count, int stockQuantity = 10)
		{
			using var context = new ApplicationDbContext(options);
			var category = new Category
			{
				Name = "Test Category",
				AvgShippingCost = 100m,
			};

			var product = new Product
			{
				Id = productId,
				Name = "Test Product",
				Description = "Product used by checkout integration tests.",
				MaxExpectation = 10,
				Category = category,
				ListPrice = 100m,
				FinalRetailPrice = 100m,
				FinalWholesalePrice = 70m,
				IsAvailableInStore = true,
				IsDeleted = false,
				StockQuantity = stockQuantity,
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

		private static void SeedTwoUserSharedStock(DbContextOptions<ApplicationDbContext> options, string user1Id, string user2Id, int productId, int count, int stockQuantity)
		{
			using var context = new ApplicationDbContext(options);
			var category = new Category
			{
				Name = "Test Category",
				AvgShippingCost = 100m,
			};

			var product = new Product
			{
				Id = productId,
				Name = "Test Product",
				Description = "Product used by concurrent stock integration tests.",
				MaxExpectation = 10,
				Category = category,
				ListPrice = 100m,
				FinalRetailPrice = 100m,
				FinalWholesalePrice = 70m,
				IsAvailableInStore = true,
				IsDeleted = false,
				StockQuantity = stockQuantity,
			};

			context.Categories.Add(category);
			context.Products.Add(product);
			context.ApplicationUsers.AddRange(
				new ApplicationUser { Id = user1Id, UserName = $"{user1Id}@example.com", Email = $"{user1Id}@example.com", Name = "User One" },
				new ApplicationUser { Id = user2Id, UserName = $"{user2Id}@example.com", Email = $"{user2Id}@example.com", Name = "User Two" });
			context.ShoppingCarts.AddRange(
				new ShoppingCart { ApplicationUserId = user1Id, ProductId = productId, Count = count },
				new ShoppingCart { ApplicationUserId = user2Id, ProductId = productId, Count = count });
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

		private static OrderHeader CreatePostedCompanyOrderHeader()
		{
			return new OrderHeader
			{
				Name = "Maria Lopez",
				StreetAddress = "Av. Corrientes 1234",
				City = "CABA",
				State = "CABA",
				PostalCode = "1043",
				PhoneNumber = "+54 11 8765-4321",
				PaymentMethod = SD.PaymentMethodBankTransfer,
			};
		}

		private static void SeedCompanyOrder(DbContextOptions<ApplicationDbContext> options, string userId, int companyId)
		{
			using var context = new ApplicationDbContext(options);
			var category = new Category
			{
				Name = "Test Category",
				AvgShippingCost = 100m,
			};

			var product = new Product
			{
				Id = 10,
				Name = "Test Product",
				Description = "Product used by checkout integration tests.",
				MaxExpectation = 10,
				Category = category,
				ListPrice = 100m,
				FinalRetailPrice = 100m,
				FinalWholesalePrice = 70m,
				IsAvailableInStore = true,
				IsDeleted = false,
				StockQuantity = 10,
			};

			var company = new Company
			{
				Id = companyId,
				Name = "Textiles SA",
				StreetAddress = "Av. Corrientes 1234",
				City = "CABA",
				State = "CABA",
				PostalCode = "1043",
				PhoneNumber = "+54 11 8765-4321",
				RazonSocial = "Textiles SA SRL",
				DomicilioFiscal = "Av. Corrientes 1234, CABA",
				Cuit = "30-71234567-9",
				IsDeleted = false,
			};

			var user = new ApplicationUser
			{
				Id = userId,
				UserName = "test@example.com",
				Email = "test@example.com",
				Name = "Maria Lopez",
				CompanyId = companyId,
			};

			context.Categories.Add(category);
			context.Products.Add(product);
			context.Companies.Add(company);
			context.ApplicationUsers.Add(user);
			context.ShoppingCarts.Add(new ShoppingCart
			{
				ApplicationUserId = userId,
				ProductId = 10,
				Count = 2,
			});
			context.SaveChanges();
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
