using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository;
using VaultShop.Models;

namespace VaultShop.Web.Tests
{
	public class ProductRepositoryTests
	{
		[Fact]
		public void Update_PersistsMaxExpectationFromAdminInput()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);

			int productId;
			int categoryId;
			using (var context = new ApplicationDbContext(options))
			{
				var category = new Category { Name = "Test Category", AvgShippingCost = 0m };
				var product = new Product
				{
					Name = "Original Name",
					Description = "Product used by repository tests.",
					MaxExpectation = 10,
					Category = category,
					ListPrice = 100m,
					FinalRetailPrice = 100m,
					FinalWholesalePrice = 100m,
					IsDeleted = false,
				};
				context.Products.Add(product);
				context.SaveChanges();
				productId = product.Id;
				categoryId = category.Id;
			}

			using (var context = new ApplicationDbContext(options))
			{
				var repo = new ProductRepository(context);
				repo.Update(new Product
				{
					Id = productId,
					Name = "Updated Name",
					MaxExpectation = 15,
					CategoryId = categoryId,
					ListPrice = 110m,
					FinalRetailPrice = 110m,
					FinalWholesalePrice = 110m,
					IsDeleted = false,
				});
				context.SaveChanges();
			}

		using (var verificationContext = new ApplicationDbContext(options))
		{
			var product = Assert.Single(verificationContext.Products.AsNoTracking());
			Assert.Equal(15, product.MaxExpectation);
			Assert.Equal("Updated Name", product.Name);
		}
	}

	[Fact]
	public void DecrementStockIfSufficient_CoveredTotal_DecrementsRelatively()
	{
		using var connection = CreateOpenConnection();
		var options = CreateOptions(connection);
		EnsureDatabaseCreated(options);
		var productId = SeedProduct(options, stockQuantity: 5);

		int affected;
		using (var context = new ApplicationDbContext(options))
		{
			affected = new ProductRepository(context).DecrementStockIfSufficient(productId, 3);
		}

		Assert.Equal(1, affected);
		using var verificationContext = new ApplicationDbContext(options);
		Assert.Equal(2, verificationContext.Products.AsNoTracking().Single(p => p.Id == productId).StockQuantity);
	}

	[Fact]
	public void DecrementStockIfSufficient_UncoveredTotal_AffectsNoRowsAndLeavesStock()
	{
		using var connection = CreateOpenConnection();
		var options = CreateOptions(connection);
		EnsureDatabaseCreated(options);
		var productId = SeedProduct(options, stockQuantity: 1);

		int affected;
		using (var context = new ApplicationDbContext(options))
		{
			affected = new ProductRepository(context).DecrementStockIfSufficient(productId, 2);
		}

		Assert.Equal(0, affected);
		using var verificationContext = new ApplicationDbContext(options);
		Assert.Equal(1, verificationContext.Products.AsNoTracking().Single(p => p.Id == productId).StockQuantity);
	}

	private static int SeedProduct(DbContextOptions<ApplicationDbContext> options, int stockQuantity)
	{
		using var context = new ApplicationDbContext(options);
		var category = new Category { Name = "Test Category", AvgShippingCost = 0m };
		var product = new Product
		{
			Name = "Stock Product",
			Description = "Product used by decrement tests.",
			MaxExpectation = 10,
			Category = category,
			ListPrice = 100m,
			FinalRetailPrice = 100m,
			FinalWholesalePrice = 100m,
			IsDeleted = false,
			IsAvailableInStore = true,
			StockQuantity = stockQuantity,
		};
		context.Products.Add(product);
		context.SaveChanges();
		return product.Id;
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