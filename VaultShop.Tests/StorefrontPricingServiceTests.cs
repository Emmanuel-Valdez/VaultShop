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

		[Fact]
		public void MixedBxGyListing_ShowsNoStrikethroughAndBadgeOnEveryProduct()
		{
			var promo = BxGy();
			promo.Name = "2da al 70%";
			promo.SameSkuOnly = false;
			var products = new[] { Product(100m, "A"), Product(200m, "B"), Product(300m, "C") };

			var displays = GetPrices(products, [promo]);

			foreach (var p in products)
			{
				Assert.False(displays[p.Id].HasDiscount);
				Assert.Equal(displays[p.Id].BasePrice, displays[p.Id].EffectivePrice);
				Assert.True(displays[p.Id].HasBadge);
				Assert.Equal("2da al 70%", displays[p.Id].Motive);
			}
		}

		[Fact]
		public void MixedBxGyListing_DirectOfferStillShowsStrikethroughAndBadge()
		{
			var now = DateTime.UtcNow;
			var offer = Product(100m, "Offered");
			offer.SaleRetailPrice = 80m;
			offer.SaleFromUtc = now.AddDays(-1);
			offer.SaleToUtc = now.AddDays(1);
			var plain = Product(200m, "Plain");
			var promo = BxGy();
			promo.Name = "2x1";
			promo.SameSkuOnly = false;

			var displays = GetPrices([offer, plain], [promo]);

			Assert.True(displays[offer.Id].HasDiscount);
			Assert.Equal(80m, displays[offer.Id].EffectivePrice);
			Assert.Equal(DiscountEvaluator.OfferMotive, displays[offer.Id].Motive);

			Assert.False(displays[plain.Id].HasDiscount);
			Assert.Equal("2x1", displays[plain.Id].Motive);
		}

		[Fact]
		public void MixedBxGyListing_CollectionPercentOfferStillShowsPerUnitDiscount()
		{
			var catA = new Category { Name = "Cat A", AvgShippingCost = 0m };
			var catB = new Category { Name = "Cat B", AvgShippingCost = 0m };
			var inCollection = Product(100m, "In Collection", catA);
			var plain = Product(200m, "Plain", catB);
			var bxgy = BxGy();
			bxgy.Name = "2x1";
			bxgy.SameSkuOnly = false;
			var percent = new Promotion
			{
				Name = "Colección -20%",
				Kind = PromotionKind.PercentOff,
				Scope = PromotionScope.Category,
				DiscountPercent = 20m,
				IsActive = true
			};

			var displays = GetPrices([inCollection, plain], [bxgy, percent]);

			Assert.True(displays[inCollection.Id].HasDiscount);
			Assert.Equal(80m, displays[inCollection.Id].EffectivePrice);
			Assert.Equal("Colección -20%", displays[inCollection.Id].Motive);

			Assert.False(displays[plain.Id].HasDiscount);
			Assert.Equal("2x1", displays[plain.Id].Motive);
		}

		[Fact]
		public void FreeShippingFlag_IsSurfacedForRetailOnly()
		{
			var free = Product();
			free.FreeShipping = true;
			var paid = Product(name: "Paid Product");

			var retail = GetPrices([free, paid], [], useWholesale: false);
			Assert.True(retail[free.Id].FreeShipping);
			Assert.False(retail[paid.Id].FreeShipping);

			// envio-gratis-o-cargo 5.2: wholesale viewers (Company / preview) never see the badge.
			var wholesale = GetPrices([free, paid], [], useWholesale: true);
			Assert.False(wholesale[free.Id].FreeShipping);
		}

		private static void AssertNoBadge(ProductDisplayPrice display)
		{
			Assert.False(display.HasBadge);
			Assert.Null(display.Motive);
		}

		private static ProductDisplayPrice GetPrice(Promotion promo, bool useWholesale = false)
		{
			var product = Product();
			return GetPrices([product], [promo], useWholesale)[product.Id];
		}

		private static IReadOnlyDictionary<int, ProductDisplayPrice> GetPrices(
			IReadOnlyList<Product> products, IReadOnlyList<Promotion> promotions, bool useWholesale = false)
		{
			using var connection = CreateOpenConnection();
			using var context = new ApplicationDbContext(CreateOptions(connection));
			context.Database.EnsureCreated();
			context.Products.AddRange(products);
			context.SaveChanges();
			// Category-scoped promotions enter scope via the product's category id.
			foreach (var promo in promotions.Where(p => p.Kind == PromotionKind.PercentOff && p.Scope == PromotionScope.Category))
				promo.CategoryId = products[0].CategoryId;
			context.Promotions.AddRange(promotions);
			context.SaveChanges();

			var service = new StorefrontPricingService(new UnitOfWork(context), new DiscountEvaluator());
			return service.GetDisplayPrices(products, useWholesale);
		}

		private static Product Product(decimal retailPrice = 100m, string name = "Storefront Product", Category? category = null) => new()
		{
			Name = name,
			MaxExpectation = 10,
			Category = category ?? new Category { Name = "Test Category", AvgShippingCost = 0m },
			FinalRetailPrice = retailPrice,
			FinalWholesalePrice = retailPrice * 0.7m,
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
