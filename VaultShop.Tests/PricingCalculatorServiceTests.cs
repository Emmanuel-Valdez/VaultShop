using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VaultShop.DataAccess.Data;
using VaultShop.Models;
using VaultShop.Models.CalculatorModels;
using VaultShop.Web.Services.Pricing;

namespace VaultShop.Web.Tests
{
	public class PricingCalculatorServiceTests
	{
		[Fact]
		public void GetTotalPercentageCostWholesale_WhenEmpty_ReturnsZero()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);

			using var context = new ApplicationDbContext(options);
			var service = new PricingCalculatorService(context);

			var result = service.GetTotalPercentageCostWholesale();

			Assert.Equal(0m, result.TotalPercentage);
		}

		[Fact]
		public void GetTotalPercentageCostWholesale_WithMultipleRows_ReturnsSum()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);

			using (var context = new ApplicationDbContext(options))
			{
				context.PercentageCostsWholesale.AddRange(
					new PercentageCostWholesale { Name = "Marketplace", Percentage = 7.5m },
					new PercentageCostWholesale { Name = "Payment Processor", Percentage = 2.5m });
				context.SaveChanges();
			}

			using var verificationContext = new ApplicationDbContext(options);
			var service = new PricingCalculatorService(verificationContext);

			var result = service.GetTotalPercentageCostWholesale();

			Assert.Equal(10m, result.TotalPercentage);
		}

		[Fact]
		public void GetFinalPrices_WithWholesalePercentageCost_UsesCombinedWholesaleFormula()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			SeedPricingScenario(
				options,
				wholesaleProfit: 20m,
				wholesalePercentageCosts: new[] { 10m });

			using var context = new ApplicationDbContext(options);
			var service = new PricingCalculatorService(context);

			var result = Assert.Single(service.GetFinalPrices());

			Assert.Equal(125m, result.WholesaleWithProfit);
			Assert.Equal(100m / (1m - 30m / 100m), result.FinalWholesale);
		}

		[Fact]
		public void GetFinalPrices_WithRetailPercentageCosts_DoesNotApplyThemToWholesale()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			SeedPricingScenario(
				options,
				wholesaleProfit: 20m,
				retailPercentageCosts: new[] { 40m });

			using var context = new ApplicationDbContext(options);
			var service = new PricingCalculatorService(context);

			var result = Assert.Single(service.GetFinalPrices());

			Assert.Equal(125m, result.WholesaleWithProfit);
			Assert.Equal(125m, result.FinalWholesale);
		}

		[Fact]
		public void PublishSuggestedPrices_WhenApprovalFlowRuns_UpdatesFinalWholesalePriceFromFinalWholesale()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			SeedPricingScenario(
				options,
				wholesaleProfit: 20m,
				wholesalePercentageCosts: new[] { 10m },
				listPrice: 100m,
				finalWholesalePrice: 50m);

			decimal suggestedFinalWholesale;
			using (var context = new ApplicationDbContext(options))
			{
				var service = new PricingCalculatorService(context);
				var suggestedPrice = Assert.Single(service.GetFinalPrices());
				suggestedFinalWholesale = suggestedPrice.FinalWholesale;

				Assert.Equal(125m, suggestedPrice.WholesaleWithProfit);
			}

			using (var verificationContext = new ApplicationDbContext(options))
			{
				var product = Assert.Single(verificationContext.Products.AsNoTracking());
				Assert.Equal(50m, product.FinalWholesalePrice);
			}

			using (var context = new ApplicationDbContext(options))
			{
				var service = new PricingCalculatorService(context);
				var updatedCount = service.PublishSuggestedPrices();

				Assert.Equal(1, updatedCount);
			}

			using (var verificationContext = new ApplicationDbContext(options))
			{
				var product = Assert.Single(verificationContext.Products.AsNoTracking());
				Assert.True(Math.Abs(suggestedFinalWholesale - product.FinalWholesalePrice) <= 0.01m);
				Assert.NotEqual(125m, product.FinalWholesalePrice);
			}
		}

		// envio-gratis 2.1 — the category shipping share is added to the suggested retail only for
		// free-shipping products (5000 at 10% costs -> +5555.56); wholesale is identical either way.
		[Fact]
		public void GetFinalPrices_ShipsShareOnlyForFreeShippingProducts()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);

			using (var context = new ApplicationDbContext(options))
			{
				var category = new Category { Name = "Test Category", AvgShippingCost = 5000m };
				context.Categories.Add(category);
				context.Products.AddRange(
					new Product
					{
						Name = "Free", Description = "Product used by pricing calculator service tests.",
						MaxExpectation = 10, Category = category, ListPrice = 100m, FinalRetailPrice = 100m,
						FinalWholesalePrice = 100m, IsDeleted = false, FreeShipping = true,
					},
					new Product
					{
						Name = "Paid", Description = "Product used by pricing calculator service tests.",
						MaxExpectation = 10, Category = category, ListPrice = 100m, FinalRetailPrice = 100m,
						FinalWholesalePrice = 100m, IsDeleted = false, FreeShipping = false,
					});
				context.FixedCosts.Add(new FixedCost { Name = "Fixed Cost", Cost = 1000m });
				context.PercentageProfits.Add(new PercentageProfit { Id = 1, Retail = 0m, Wholesale = 0m });
				context.PercentageCosts.Add(new PercentageCost { Name = "Retail Percentage Cost 10", Percentage = 10m });
				context.SaveChanges();
			}

			using var verificationContext = new ApplicationDbContext(options);
			var service = new PricingCalculatorService(verificationContext);

			var prices = service.GetFinalPrices().ToDictionary(price => price.Product.Name);
			var free = prices["Free"];
			var paid = prices["Paid"];

			Assert.Equal(100m, free.RetailWithProfit);
			Assert.Equal(5100m, free.RetailWithShipping);
			Assert.Equal(100m, paid.RetailWithShipping);
			Assert.Equal(5555.56m, Math.Round(free.FinalRetail - paid.FinalRetail, 2));
			Assert.Equal(free.FinalWholesale, paid.FinalWholesale);
		}

		// envio-gratis 2.2 — a backfilled (free-shipping) product whose price already reflects the
		// with-shipping suggestion stays current after deploy; an unflagged product drops to the
		// shipping-free suggestion and publishes it, leaving wholesale untouched.
		[Fact]
		public void OutdatedCountAndPublish_RespectFreeShippingFlag()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);

			const decimal withShippingRetail = 5666.67m; // (100 + 5000) / 0.9

			using (var context = new ApplicationDbContext(options))
			{
				var category = new Category { Name = "Test Category", AvgShippingCost = 5000m };
				context.Categories.Add(category);
				context.Products.AddRange(
					new Product
					{
						Name = "Free", Description = "Product used by pricing calculator service tests.",
						MaxExpectation = 10, Category = category, ListPrice = withShippingRetail,
						FinalRetailPrice = withShippingRetail, FinalWholesalePrice = 100m, IsDeleted = false,
						FreeShipping = true,
					},
					new Product
					{
						Name = "Paid", Description = "Product used by pricing calculator service tests.",
						MaxExpectation = 10, Category = category, ListPrice = withShippingRetail,
						FinalRetailPrice = withShippingRetail, FinalWholesalePrice = 100m, IsDeleted = false,
						FreeShipping = false,
					});
				context.FixedCosts.Add(new FixedCost { Name = "Fixed Cost", Cost = 1000m });
				context.PercentageProfits.Add(new PercentageProfit { Id = 1, Retail = 0m, Wholesale = 0m });
				context.PercentageCosts.Add(new PercentageCost { Name = "Retail Percentage Cost 10", Percentage = 10m });
				context.SaveChanges();
			}

			using (var context = new ApplicationDbContext(options))
			{
				var service = new PricingCalculatorService(context);
				Assert.Equal(1, service.GetOutdatedProductCount());
				Assert.Equal(1, service.PublishSuggestedPrices());
			}

			using (var verificationContext = new ApplicationDbContext(options))
			{
				var products = verificationContext.Products.AsNoTracking().ToDictionary(product => product.Name);
				Assert.Equal(withShippingRetail, products["Free"].ListPrice);
				Assert.True(Math.Abs(products["Paid"].ListPrice - 100m / 0.9m) <= 0.01m);
				Assert.Equal(100m, products["Free"].FinalWholesalePrice);
				Assert.Equal(100m, products["Paid"].FinalWholesalePrice);
			}
		}

		[Fact]
		public void GetFinalPrices_WithInvalidCombinedWholesalePercentage_ThrowsClearException()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			SeedPricingScenario(
				options,
				wholesaleProfit: 90m,
				wholesalePercentageCosts: new[] { 10m });

			using var context = new ApplicationDbContext(options);
			var service = new PricingCalculatorService(context);

			var exception = Assert.Throws<InvalidOperationException>(() => service.GetFinalPrices());

			Assert.Contains("combined wholesale percentage", exception.Message);
			Assert.Contains("less than 100", exception.Message);
		}

		[Fact]
		public void GetCostByProducts_ProductsInSameCategoryWithDifferentExpectations_HaveDifferentFixedCostShares()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);

			using (var context = new ApplicationDbContext(options))
			{
				var category = new Category
				{
					Name = "Test Category",
					AvgShippingCost = 0m,
				};

				context.Categories.Add(category);
				context.Products.AddRange(
					new Product
					{
						Name = "Low Expectation",
						Description = "Product used by pricing calculator service tests.",
						MaxExpectation = 10,
						Category = category,
						ListPrice = 100m,
						FinalRetailPrice = 100m,
						FinalWholesalePrice = 100m,
						IsDeleted = false,
					},
					new Product
					{
						Name = "High Expectation",
						Description = "Product used by pricing calculator service tests.",
						MaxExpectation = 20,
						Category = category,
						ListPrice = 200m,
						FinalRetailPrice = 200m,
						FinalWholesalePrice = 200m,
						IsDeleted = false,
					});
				context.FixedCosts.Add(new FixedCost
				{
					Name = "Fixed Cost",
					Cost = 1000m,
				});
				context.SaveChanges();
			}

			using var verificationContext = new ApplicationDbContext(options);
			var service = new PricingCalculatorService(verificationContext);

			var result = service.GetCostByProducts().ToDictionary(product => product.Product.Name);

			Assert.Equal(100m, result["Low Expectation"].FixedCostAddedByCategory);
			Assert.Equal(10, result["Low Expectation"].MaxExpectationMonthly);
			Assert.Equal(50m, result["High Expectation"].FixedCostAddedByCategory);
			Assert.Equal(20, result["High Expectation"].MaxExpectationMonthly);
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

		private static void SeedPricingScenario(
			DbContextOptions<ApplicationDbContext> options,
			decimal retailProfit = 0m,
			decimal wholesaleProfit = 0m,
			IEnumerable<decimal>? retailPercentageCosts = null,
			IEnumerable<decimal>? wholesalePercentageCosts = null,
			decimal listPrice = 100m,
			decimal finalWholesalePrice = 100m)
		{
			using var context = new ApplicationDbContext(options);

			var category = new Category
			{
				Name = "Test Category",
				AvgShippingCost = 0m,
			};

			context.Categories.Add(category);
			context.Products.Add(new Product
			{
				Name = "Test Product",
				Description = "Product used by pricing calculator service tests.",
				MaxExpectation = 10,
				Category = category,
				ListPrice = listPrice,
				FinalRetailPrice = listPrice,
				FinalWholesalePrice = finalWholesalePrice,
				IsDeleted = false,
			});
			context.FixedCosts.Add(new FixedCost
			{
				Name = "Fixed Cost",
				Cost = 1000m,
			});
			context.PercentageProfits.Add(new PercentageProfit
			{
				Id = 1,
				Retail = retailProfit,
				Wholesale = wholesaleProfit,
			});

			foreach (var retailPercentageCost in retailPercentageCosts ?? Enumerable.Empty<decimal>())
			{
				context.PercentageCosts.Add(new PercentageCost
				{
					Name = $"Retail Percentage Cost {retailPercentageCost}",
					Percentage = retailPercentageCost,
				});
			}

			foreach (var wholesalePercentageCost in wholesalePercentageCosts ?? Enumerable.Empty<decimal>())
			{
				context.PercentageCostsWholesale.Add(new PercentageCostWholesale
				{
					Name = $"Wholesale Percentage Cost {wholesalePercentageCost}",
					Percentage = wholesalePercentageCost,
				});
			}

			context.SaveChanges();
		}
	}
}
