using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VaultShop.DataAccess.Data;
using VaultShop.Models;

namespace VaultShop.Web.Tests
{
	// descuentos-promociones §1: no active discounts by default; pre-discount
	// orders read back with zero discount totals.
	public class DiscountModelDefaultsTests
	{
		[Fact]
		public void NewDatabase_HasNoActiveDiscounts()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);

			using var context = new ApplicationDbContext(options);
			Assert.Empty(context.Coupons.Where(c => c.IsActive));
			Assert.Empty(context.Promotions.Where(p => p.IsActive));
		}

		[Fact]
		public void Product_WithoutOffer_HasNullSaleFields()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);

			int productId;
			using (var context = new ApplicationDbContext(options))
			{
				context.Products.Add(new Product
				{
					Name = "Plain Product",
					Description = "No offer set.",
					MaxExpectation = 10,
					Category = new Category { Name = "Test Category", AvgShippingCost = 0m },
					ListPrice = 100m,
					FinalRetailPrice = 100m,
					FinalWholesalePrice = 70m,
				});
				context.SaveChanges();
				productId = context.Products.Single().Id;
			}

			using var verificationContext = new ApplicationDbContext(options);
			var product = verificationContext.Products.AsNoTracking().Single(p => p.Id == productId);
			Assert.Null(product.SaleRetailPrice);
			Assert.Null(product.SaleWholesalePrice);
			Assert.Null(product.SaleFromUtc);
			Assert.Null(product.SaleToUtc);
		}

		[Fact]
		public void Order_WithoutDiscounts_ReadsBackZeroTotals()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);

			int orderId;
			using (var context = new ApplicationDbContext(options))
			{
				context.ApplicationUsers.Add(new ApplicationUser { Id = "user-1", UserName = "test@example.com" });
				var product = new Product
				{
					Name = "Ordered Product",
					Description = "Pre-discount order line.",
					MaxExpectation = 10,
					Category = new Category { Name = "Test Category", AvgShippingCost = 0m },
					ListPrice = 100m,
					FinalRetailPrice = 100m,
					FinalWholesalePrice = 70m,
				};
				var header = new OrderHeader
				{
					ApplicationUserId = "user-1",
					OrderDate = DateTime.UtcNow,
					ShippingDate = DateTime.UtcNow,
					OrderTotal = 200m,
					Name = "Test User",
					StreetAddress = "Street 123",
					City = "City",
					State = "State",
					PostalCode = "1234",
					PhoneNumber = "123",
				};
				context.Products.Add(product);
				context.OrderHeaders.Add(header);
				context.SaveChanges();
				context.OrderDetails.Add(new OrderDetail
				{
					OrderHeaderId = header.Id,
					ProductId = product.Id,
					Count = 2,
					Price = 100m,
				});
				context.SaveChanges();
				orderId = header.Id;
			}

			using var verificationContext = new ApplicationDbContext(options);
			var headerRead = verificationContext.OrderHeaders.AsNoTracking().Single(h => h.Id == orderId);
			Assert.Equal(0m, headerRead.DiscountTotal);
			Assert.Equal(0m, headerRead.PaymentDiscountTotal);
			Assert.Null(headerRead.CouponCode);
			Assert.Null(headerRead.AppliedPromotionIds);

			var detailRead = verificationContext.OrderDetails.AsNoTracking().Single();
			Assert.Equal(0m, detailRead.OriginalPrice);
			Assert.Equal(0m, detailRead.DiscountAmount);
			Assert.Null(detailRead.DiscountMotive);
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
	}
}
