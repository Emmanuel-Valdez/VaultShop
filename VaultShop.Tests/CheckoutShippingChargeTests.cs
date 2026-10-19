using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository;
using VaultShop.Models;
using VaultShop.Models.CalculatorModels;
using VaultShop.Utility;
using VaultShop.Web.Services.Checkout;
using VaultShop.Web.Services.Pricing;
using VaultShop.Web.Services.ProductVariants;

namespace VaultShop.Web.Tests
{
	// envio-gratis-o-cargo §3 — charge + free qualification + frozen total + session shipping line.
	public class CheckoutShippingChargeTests
	{
		[Fact]
		public void GetShippingQuote_AnyFreeLine_MakesOrderFree()
		{
			using var h = new Harness(10m);
			using var context = h.NewContext();
			var service = h.CreateService(context);

			var quote = service.GetShippingQuote(
				[Cart(5000m, freeShipping: false), Cart(2000m, freeShipping: true)],
				useWholesalePrice: false);

			Assert.True(quote.QualifiesForFreeShipping);
			Assert.Equal(0m, quote.Charge);
			// Struck-through standard = largest category average (5000) grossed up at 10%.
			Assert.Equal(5555.56m, quote.StandardAmount);
		}

		[Fact]
		public void GetShippingQuote_PaidOnly_ChargesLargestAverageOnceNotPerUnitOrLine()
		{
			using var h = new Harness(10m);
			using var context = h.NewContext();
			var service = h.CreateService(context);

			var quote = service.GetShippingQuote(
				[Cart(2000m, freeShipping: false, count: 3), Cart(5000m, freeShipping: false, count: 1)],
				useWholesalePrice: false);

			Assert.False(quote.QualifiesForFreeShipping);
			// 5000 grossed up once (not 2000 + 5000, not per unit/line).
			Assert.Equal(5555.56m, quote.Charge);
		}

		[Fact]
		public void GetShippingQuote_Wholesale_NeverCharges()
		{
			using var h = new Harness(10m);
			using var context = h.NewContext();
			var service = h.CreateService(context);

			var quote = service.GetShippingQuote([Cart(5000m, freeShipping: false)], useWholesalePrice: true);

			Assert.False(quote.QualifiesForFreeShipping);
			Assert.Equal(0m, quote.Charge);
		}

		[Fact]
		public void CreateOrder_FreezesChargeAndCouponDoesNotAlterIt()
		{
			using var h = new Harness(10m);
			SeedProduct(h, avgShippingCost: 5000m, freeShipping: false, price: 10000m, count: 1);
			SeedCoupon(h, "OFF10", CouponDiscountType.Percent, 10m);

			int orderId;
			using (var context = h.NewContext())
			{
				var service = h.CreateService(context);
				var result = service.CreateOrder("user-1",
					new OrderHeader { PaymentMethod = SD.PaymentMethodStripe },
					useWholesalePrice: false, couponCode: "OFF10");
				Assert.True(result.OrderId > 0);
				orderId = result.OrderId!.Value;
			}

			using var verify = h.NewContext();
			var order = verify.OrderHeaders.AsNoTracking().Single(o => o.Id == orderId);
			// Discounted product total (10000 - 1000) + untouched charge (5555.56).
			Assert.Equal(5555.56m, order.ShippingCharge);
			Assert.Equal(14555.56m, order.OrderTotal);
		}

		[Fact]
		public void CreateOrder_FullyDiscountedFreeOrder_StillHitsOrderTotalGuard()
		{
			using var h = new Harness();
			SeedProduct(h, avgShippingCost: 100m, freeShipping: true, price: 1000m, count: 1);
			SeedCoupon(h, "FREE", CouponDiscountType.FixedAmount, 1000m);

			using var context = h.NewContext();
			var service = h.CreateService(context);
			var result = service.CreateOrder("user-1",
				new OrderHeader { PaymentMethod = SD.PaymentMethodStripe },
				useWholesalePrice: false, couponCode: "FREE");

			Assert.True(result.OrderTotalInvalid);
		}

		[Fact]
		public void BuildPaymentSessionLineItems_WithoutDiscount_SumsToOrderTotal()
		{
			using var h = new Harness(10m);
			SeedProduct(h, avgShippingCost: 5000m, freeShipping: false, price: 100m, count: 2);

			using var context = h.NewContext();
			var service = h.CreateService(context);
			var result = service.CreateOrder("user-1",
				new OrderHeader { PaymentMethod = SD.PaymentMethodStripe }, useWholesalePrice: false);
			var vm = result.ShoppingCartVM!;

			var lines = service.BuildPaymentSessionLineItems(vm);

			Assert.Equal(vm.OrderHeader.OrderTotal, lines.Sum(l => l.UnitPrice * l.Quantity));
			Assert.Contains(lines, l => l.Quantity == 1 && l.UnitPrice == 5555.56m);
		}

		[Fact]
		public void BuildPaymentSessionLineItems_WithPaymentDiscount_SumsToOrderTotal()
		{
			using var h = new Harness(10m);
			SeedProduct(h, avgShippingCost: 5000m, freeShipping: false, price: 10000m, count: 1);
			SeedPaymentPromo(h, SD.PaymentMethodBankTransfer, 10m);

			using var context = h.NewContext();
			var service = h.CreateService(context);
			var result = service.CreateOrder("user-1",
				new OrderHeader { PaymentMethod = SD.PaymentMethodBankTransfer }, useWholesalePrice: false);
			var vm = result.ShoppingCartVM!;

			var lines = service.BuildPaymentSessionLineItems(vm);

			Assert.True(vm.OrderHeader.PaymentDiscountTotal > 0);
			Assert.Equal(vm.OrderHeader.OrderTotal, lines.Sum(l => l.UnitPrice * l.Quantity));
		}

		private static ShoppingCart Cart(decimal avgShippingCost, bool freeShipping, int count = 1)
		{
			var category = new Category { Id = 1, Name = "Cat", AvgShippingCost = avgShippingCost };
			var product = new Product
			{
				Id = 1, Name = "P", FreeShipping = freeShipping,
				Category = category, FinalRetailPrice = 100m,
			};
			return new ShoppingCart { ProductId = 1, Product = product, Count = count };
		}

		private static void SeedProduct(Harness h, decimal avgShippingCost, bool freeShipping, decimal price, int count, string userId = "user-1")
		{
			using var context = h.NewContext();
			var category = new Category { Name = "Cat", AvgShippingCost = avgShippingCost };
			var product = new Product
			{
				Name = freeShipping ? "Free Product" : "Paid Product",
				Description = "Product for checkout shipping charge tests.",
				MaxExpectation = 10,
				Category = category,
				ListPrice = price,
				FinalRetailPrice = price,
				FinalWholesalePrice = price * 0.7m,
				IsAvailableInStore = true,
				IsDeleted = false,
				StockQuantity = 100,
				FreeShipping = freeShipping,
			};
			context.Categories.Add(category);
			context.Products.Add(product);
			if (!context.ApplicationUsers.Any(u => u.Id == userId))
			{
				context.ApplicationUsers.Add(new ApplicationUser { Id = userId, UserName = "u1@test.local", Name = "Test User" });
			}
			context.ShoppingCarts.Add(new ShoppingCart { ApplicationUserId = userId, Product = product, Count = count });
			context.SaveChanges();
		}

		private static void SeedCoupon(Harness h, string code, CouponDiscountType type, decimal value)
		{
			using var context = h.NewContext();
			context.Coupons.Add(new Coupon { Code = code, DiscountType = type, Value = value, IsActive = true });
			context.SaveChanges();
		}

		private static void SeedPaymentPromo(Harness h, string paymentMethod, decimal percent)
		{
			using var context = h.NewContext();
			context.Promotions.Add(new Promotion
			{
				Name = $"{paymentMethod} -{percent}%",
				Kind = PromotionKind.PaymentMethodDiscount,
				Scope = PromotionScope.Store,
				PaymentMethod = paymentMethod,
				DiscountPercent = percent,
				IsActive = true,
			});
			context.SaveChanges();
		}

		private sealed class Harness : IDisposable
		{
			private readonly SqliteConnection _connection;

			public DbContextOptions<ApplicationDbContext> Options { get; }

			public Harness(params decimal[] retailPercentageCosts)
			{
				_connection = new SqliteConnection("Data Source=:memory:");
				_connection.Open();
				Options = new DbContextOptionsBuilder<ApplicationDbContext>()
					.UseSqlite(_connection)
					.Options;

				using var context = new ApplicationDbContext(Options);
				context.Database.EnsureCreated();
				foreach (var percentage in retailPercentageCosts)
				{
					context.PercentageCosts.Add(new PercentageCost { Name = $"Retail {percentage}", Percentage = percentage });
				}
				context.SaveChanges();
			}

			public ApplicationDbContext NewContext() => new(Options);

			public CheckoutService CreateService(ApplicationDbContext context)
			{
				var unitOfWork = new UnitOfWork(context);
				return new CheckoutService(unitOfWork, NullLogger<CheckoutService>.Instance,
					new ProductVariantService(unitOfWork), new DiscountEvaluator());
			}

			public void Dispose() => _connection.Dispose();
		}
	}
}
