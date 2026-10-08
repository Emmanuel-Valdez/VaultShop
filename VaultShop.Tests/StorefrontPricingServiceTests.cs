using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository;
using VaultShop.Models;
using VaultShop.Web.Services.Pricing;

namespace VaultShop.Web.Tests
{
	// P1.3: badge coverage runs the same grantable rule as the evaluator, so an
	// inactive or zero-quantity BxGy promotion never promises a cart benefit.
	public class StorefrontPricingServiceTests
	{
		[Fact]
		public void ActiveBxGy_ShowsBadge()
		{
			var display = GetPrice(BxGy());

			Assert.False(display.HasDiscount);
			Assert.True(display.HasBadge);
			Assert.Equal("2x1", display.Motive);
		}

		[Fact]
		public void InactiveBxGy_ShowsNoBadge()
		{
			var promo = BxGy();
			promo.IsActive = false;

			AssertNoBadge(GetPrice(promo));
		}

		[Fact]
		public void ZeroQuantityBxGy_ShowsNoBadge()
		{
			AssertNoBadge(GetPrice(BxGy(buy: 0)));
			AssertNoBadge(GetPrice(BxGy(get: 0)));
		}

		[Fact]
		public void ZeroPercentBxGy_ShowsNoBadge()
		{
			AssertNoBadge(GetPrice(BxGy(getPct: 0m)));
		}

		[Fact]
		public void RetailOnlyBxGy_HidesBadgeForWholesaleViewer()
		{
			var promo = BxGy();
			promo.IncludeWholesale = false;

			AssertNoBadge(GetPrice(promo, useWholesale: true));
		}

		[Fact]
		public void WholesaleOptInBxGy_ShowsBadgeForRetailViewer()
		{
			var promo = BxGy();
			promo.IncludeWholesale = true;

			Assert.True(GetPrice(promo, useWholesale: false).HasBadge);
		}

		private static void AssertNoBadge(ProductDisplayPrice display)
		{
			Assert.False(display.HasBadge);
			Assert.Null(display.Motive);
		}

		private static ProductDisplayPrice GetPrice(Promotion promo, bool useWholesale = false)
		{
			using var connection = CreateOpenConnection();
			using var context = new ApplicationDbContext(CreateOptions(connection));
			context.Database.EnsureCreated();
			var product = Product();
			context.Products.Add(product);
			context.Promotions.Add(promo);
			context.SaveChanges();

			var service = new StorefrontPricingService(new UnitOfWork(context), new DiscountEvaluator());
			return service.GetDisplayPrices([product], useWholesale)[product.Id];
		}

		private static Product Product() => new()
		{
			Name = "Storefront Product",
			MaxExpectation = 10,
			Category = new Category { Name = "Test Category", AvgShippingCost = 0m },
			FinalRetailPrice = 100m,
			FinalWholesalePrice = 70m,
			IsAvailableInStore = true,
			StockQuantity = 10
		};

		private static Promotion BxGy(int buy = 1, int get = 1, decimal getPct = 100m) => new()
		{
			Name = "2x1",
			Kind = PromotionKind.BxGy,
			Scope = PromotionScope.Store,
			BuyQty = buy,
			GetQty = get,
			GetDiscountPercent = getPct,
			IsActive = true
		};

		private static SqliteConnection CreateOpenConnection()
		{
			var connection = new SqliteConnection("Data Source=:memory:");
			connection.Open();
			return connection;
		}

		private static DbContextOptions<ApplicationDbContext> CreateOptions(SqliteConnection connection)
			=> new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
	}
}
