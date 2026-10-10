using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository;
using VaultShop.Models;
using VaultShop.Utility;
using VaultShop.Web.Services.Checkout;
using VaultShop.Web.Services.Pricing;
using VaultShop.Web.Services.ProductVariants;
using static VaultShop.Web.Services.Checkout.ICheckoutService;

namespace VaultShop.Web.Tests
{
	// descuentos-promociones §3: evaluator wired into summary + order creation —
	// frozen breakdown, coupon use increment, payment-method lines.
	public class CheckoutDiscountTests
	{
		[Fact]
		public void BuildSummary_WithOffer_ShowsDiscountedPriceAndMotive()
		{
			var options = CreateOptions(CreateOpenConnection());
			EnsureDatabaseCreated(options);
			SeedProduct(options, saleRetail: 8000m);

			CheckoutSummaryResult result;
			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				result = service.BuildSummary("user-1", useWholesalePrice: false);
			}

			var cart = Assert.Single(result.ShoppingCartVM!.ShoppingCartList);
			Assert.Equal(8000m, cart.Price);
			Assert.Equal(10000m, cart.OriginalPrice);
			Assert.Equal(2000m, cart.DiscountAmount);
			Assert.Equal(DiscountEvaluator.OfferMotive, cart.DiscountMotive);
			Assert.Equal(8000m, result.ShoppingCartVM.OrderHeader.OrderTotal);
			Assert.Equal(2000m, result.ShoppingCartVM.OrderHeader.DiscountTotal);
		}

		[Fact]
		public void CreateOrder_WithOfferAndTransfer_FreezesBreakdownWithPaymentLine()
		{
			var options = CreateOptions(CreateOpenConnection());
			EnsureDatabaseCreated(options);
			SeedProduct(options, saleRetail: 8000m);
			SeedPaymentPromo(options, "BankTransfer", 10m);

			int orderId;
			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				var result = service.CreateOrder("user-1",
					new OrderHeader { PaymentMethod = SD.PaymentMethodBankTransfer },
					useWholesalePrice: false);

				Assert.False(result.CouponDroppedAtCreation);
				orderId = result.OrderId!.Value;
			}

			using var verify = new ApplicationDbContext(options);
			var header = verify.OrderHeaders.AsNoTracking().Single(h => h.Id == orderId);
			Assert.Equal(2000m, header.DiscountTotal);
			Assert.Equal(800m, header.PaymentDiscountTotal);
			Assert.Equal("BankTransfer -10%", header.PaymentDiscountMotive);
			Assert.Equal(7200m, header.OrderTotal);
			var detail = verify.OrderDetails.AsNoTracking().Single();
			Assert.Equal(10000m, detail.OriginalPrice);
			Assert.Equal(8000m, detail.Price);
			Assert.Equal(2000m, detail.DiscountAmount);
			Assert.Equal(DiscountEvaluator.OfferMotive, detail.DiscountMotive);
		}

		[Fact]
		public void Order_FrozenBreakdown_SurvivesPromotionAndCouponDeletion()
		{
			var options = CreateOptions(CreateOpenConnection());
			EnsureDatabaseCreated(options);
			SeedProduct(options, price: 10000m);
			SeedPaymentPromo(options, "BankTransfer", 10m);
			SeedCoupon(options, "FROZEN10", CouponDiscountType.Percent, 10m);

			int orderId;
		using (var context = new ApplicationDbContext(options))
		{
				orderId = CreateService(context).CreateOrder("user-1",
					new OrderHeader { PaymentMethod = SD.PaymentMethodBankTransfer },
					useWholesalePrice: false, couponCode: "FROZEN10").OrderId!.Value;
		}

			using (var context = new ApplicationDbContext(options))
			{
				context.Promotions.RemoveRange(context.Promotions);
				context.Coupons.RemoveRange(context.Coupons);
				context.SaveChanges();
			}

			using var verify = new ApplicationDbContext(options);
			var header = verify.OrderHeaders.AsNoTracking().Single(h => h.Id == orderId);
			var detail = verify.OrderDetails.AsNoTracking().Single(d => d.OrderHeaderId == orderId);
			Assert.Equal("FROZEN10", header.CouponCode);
			Assert.Equal(1000m, header.DiscountTotal);
			Assert.Equal(900m, header.PaymentDiscountTotal);
			Assert.Equal("BankTransfer -10%", header.PaymentDiscountMotive);
			Assert.Equal("Cupón FROZEN10", detail.DiscountMotive);
			Assert.Equal(10000m, detail.OriginalPrice);
			Assert.Equal(9000m, detail.Price);
		}

		[Fact]
		public void CreateOrder_WithStripe_ShowsNoPaymentDiscount()
		{
			var options = CreateOptions(CreateOpenConnection());
			EnsureDatabaseCreated(options);
			SeedProduct(options, saleRetail: 8000m);
			SeedPaymentPromo(options, "BankTransfer", 10m);

			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				var result = service.CreateOrder("user-1",
					new OrderHeader { PaymentMethod = SD.PaymentMethodStripe },
					useWholesalePrice: false);

				Assert.Equal(8000m, result.ShoppingCartVM!.OrderHeader.OrderTotal);
				Assert.Equal(0m, result.ShoppingCartVM.OrderHeader.PaymentDiscountTotal);
			}
		}

		[Fact]
		public void CreateOrder_WithCoupon_FreezesCodeAndIncrementsUses()
		{
			var options = CreateOptions(CreateOpenConnection());
			EnsureDatabaseCreated(options);
			SeedProduct(options, price: 50000m);
			SeedCoupon(options, "BIENVENIDA10", CouponDiscountType.Percent, 10m, minSubtotal: 20000m);

			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				var result = service.CreateOrder("user-1",
					new OrderHeader { PaymentMethod = SD.PaymentMethodStripe },
					useWholesalePrice: false, couponCode: "bienvenida10");

				Assert.False(result.CouponDroppedAtCreation);
				Assert.Equal(45000m, result.ShoppingCartVM!.OrderHeader.OrderTotal);
			}

			using var verify = new ApplicationDbContext(options);
			var header = verify.OrderHeaders.AsNoTracking().Single();
			Assert.Equal("BIENVENIDA10", header.CouponCode);
			Assert.Equal(5000m, header.DiscountTotal);
			Assert.Equal("Cupón BIENVENIDA10", verify.OrderDetails.AsNoTracking().Single().DiscountMotive);
			Assert.Equal(1, verify.Coupons.AsNoTracking().Single().UsesCount);
		}

		[Fact]
		public void CreateOrder_CouponExhaustedAtCreation_ProceedsUndiscounted()
		{
			var options = CreateOptions(CreateOpenConnection());
			EnsureDatabaseCreated(options);
			SeedProduct(options, price: 50000m);
			SeedCoupon(options, "ONEUSE", CouponDiscountType.Percent, 10m, maxUses: 1, uses: 1);

			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				// Evaluation at apply time happens against a stale read; creation re-checks the tracked row.
				var result = service.CreateOrder("user-1",
					new OrderHeader { PaymentMethod = SD.PaymentMethodStripe },
					useWholesalePrice: false, couponCode: "ONEUSE");

				Assert.True(result.CouponDroppedAtCreation);
				Assert.Equal(50000m, result.ShoppingCartVM!.OrderHeader.OrderTotal);
				Assert.Null(result.ShoppingCartVM.OrderHeader.CouponCode);
			}
		}

		[Fact]
		public void CouponTryIncrementUses_AllowsOnlyOneWinner()
		{
			var options = CreateOptions(CreateOpenConnection());
			EnsureDatabaseCreated(options);
			SeedCoupon(options, "RACE", CouponDiscountType.Percent, 10m, maxUses: 1);

			using var context = new ApplicationDbContext(options);
			var coupon = context.Coupons.Single(c => c.Code == "RACE");
			var repository = new CouponRepository(context);

			Assert.Equal(1, repository.TryIncrementUses(coupon.Id));
			Assert.Equal(0, repository.TryIncrementUses(coupon.Id));
		}

		[Fact]
		public void FrozenLines_SumToHeaderTotal()
		{
			// P0.3: 300 base over 3 units (2+1 lines, same product) with a 1-peso
			// fixed coupon → 299 total; whole-cent units summing exactly to header.
			var options = CreateOptions(CreateOpenConnection());
			EnsureDatabaseCreated(options);
			using (var seed = new ApplicationDbContext(options))
			{
				var product = new Product
				{
					Name = "Rounding Product",
					Description = "Used by frozen-lines rounding test.",
					MaxExpectation = 10,
					Category = new Category { Name = "Test Category", AvgShippingCost = 0m },
					ListPrice = 100m,
					FinalRetailPrice = 100m,
					FinalWholesalePrice = 70m,
					IsAvailableInStore = true,
					StockQuantity = 10,
				};
				seed.Products.Add(product);
				seed.ApplicationUsers.Add(new ApplicationUser
				{
					Id = "user-1", UserName = "test@example.com", Name = "Test User"
				});
				seed.ShoppingCarts.Add(new ShoppingCart
				{
					ApplicationUserId = "user-1", Product = product, Count = 2
				});
				seed.ShoppingCarts.Add(new ShoppingCart
				{
					ApplicationUserId = "user-1", Product = product, Count = 1
				});
				seed.Coupons.Add(new Coupon
				{
					Code = "ONEPESO", DiscountType = CouponDiscountType.FixedAmount,
					Value = 1m, IsActive = true
				});
				seed.SaveChanges();
			}

			int orderId;
			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				var result = service.CreateOrder("user-1",
					new OrderHeader { PaymentMethod = SD.PaymentMethodStripe },
					useWholesalePrice: false, couponCode: "ONEPESO");

				Assert.False(result.CouponDroppedAtCreation);
				orderId = result.OrderId!.Value;
			}

			using var verify = new ApplicationDbContext(options);
			var header = verify.OrderHeaders.AsNoTracking().Single(h => h.Id == orderId);
			var details = verify.OrderDetails.AsNoTracking().Where(d => d.OrderHeaderId == orderId).ToList();
			Assert.Equal(299m, header.OrderTotal);
			foreach (var d in details)
				Assert.Equal(Math.Round(d.Price, 2, MidpointRounding.AwayFromZero), d.Price);
			Assert.Equal(header.OrderTotal, details.Sum(d => d.Price * d.Count));
		}

		[Fact]
		public void SessionLines_WithTransferDiscount_SumExactlyToOrderTotal()
		{
			var options = CreateOptions(CreateOpenConnection());
			EnsureDatabaseCreated(options);
			SeedProduct(options, price: 10000m);
			AddProductWithCart(options, "Second Product", price: 3333m, count: 3);
			SeedPaymentPromo(options, "BankTransfer", 10m);

			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				var result = service.CreateOrder("user-1",
					new OrderHeader { PaymentMethod = SD.PaymentMethodBankTransfer },
					useWholesalePrice: false);

				var header = result.ShoppingCartVM!.OrderHeader;
				Assert.Equal(1999.90m, header.PaymentDiscountTotal);
				Assert.Equal(17999.10m, header.OrderTotal);
				var lines = service.BuildPaymentSessionLineItems(result.ShoppingCartVM!);
				Assert.Equal(2, lines.Count);
				Assert.Equal(header.OrderTotal, lines.Sum(l => l.UnitPrice * l.Quantity));
				Assert.True(lines.Sum(l => l.UnitPrice * l.Quantity) < 19999m);
			}

			// Persisted rows keep pre-payment unit prices; only the session request is prorated.
			using var verify = new ApplicationDbContext(options);
			var details = verify.OrderDetails.AsNoTracking().ToList().OrderBy(d => d.Price).ToList();
			Assert.Equal(2, details.Count);
			Assert.Equal(3333m, details[0].Price);
			Assert.Equal(10000m, details[1].Price);
		}

		[Fact]
		public void SessionLines_WithoutPaymentDiscount_MatchCartLinesExactly()
		{
			var options = CreateOptions(CreateOpenConnection());
			EnsureDatabaseCreated(options);
			SeedProduct(options, price: 10000m);

			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				var result = service.CreateOrder("user-1",
					new OrderHeader { PaymentMethod = SD.PaymentMethodStripe },
					useWholesalePrice: false);

				var line = Assert.Single(service.BuildPaymentSessionLineItems(result.ShoppingCartVM!));
				Assert.Equal("Discounted Product", line.ProductName);
				Assert.Equal(10000m, line.UnitPrice);
				Assert.Equal(1, line.Quantity);
			}
		}

		private static CheckoutService CreateService(ApplicationDbContext context)
		{
			var unitOfWork = new UnitOfWork(context);
			return new CheckoutService(unitOfWork, NullLogger<CheckoutService>.Instance,
				new ProductVariantService(unitOfWork), new DiscountEvaluator());
		}

		private static void SeedProduct(DbContextOptions<ApplicationDbContext> options,
			decimal price = 10000m, decimal? saleRetail = null)
		{
			using var context = new ApplicationDbContext(options);
			var product = new Product
			{
				Name = "Discounted Product",
				Description = "Used by checkout discount tests.",
				MaxExpectation = 10,
				Category = new Category { Name = "Test Category", AvgShippingCost = 0m },
				ListPrice = price,
				FinalRetailPrice = price,
				FinalWholesalePrice = price - 30m,
				IsAvailableInStore = true,
				StockQuantity = 10,
				SaleRetailPrice = saleRetail,
				SaleFromUtc = saleRetail.HasValue ? DateTime.UtcNow.AddDays(-1) : null,
				SaleToUtc = saleRetail.HasValue ? DateTime.UtcNow.AddDays(1) : null,
			};
			context.Products.Add(product);
			context.ApplicationUsers.Add(new ApplicationUser
			{
				Id = "user-1", UserName = "test@example.com", Name = "Test User"
			});
			context.ShoppingCarts.Add(new ShoppingCart
			{
				ApplicationUserId = "user-1", Product = product, Count = 1
			});
			context.SaveChanges();
		}

		private static void SeedCoupon(DbContextOptions<ApplicationDbContext> options,
			string code, CouponDiscountType type, decimal value,
			decimal? minSubtotal = null, int? maxUses = null, int uses = 0)
		{
			using var context = new ApplicationDbContext(options);
			context.Coupons.Add(new Coupon
			{
				Code = code, DiscountType = type, Value = value,
				MinSubtotal = minSubtotal, MaxUses = maxUses, UsesCount = uses, IsActive = true
			});
			context.SaveChanges();
		}

		private static void SeedPaymentPromo(DbContextOptions<ApplicationDbContext> options,
			string paymentMethod, decimal percent)
		{
			using var context = new ApplicationDbContext(options);
			context.Promotions.Add(new Promotion
			{
				Name = $"{paymentMethod} -{percent}%",
				Kind = PromotionKind.PaymentMethodDiscount,
				Scope = PromotionScope.Store,
				PaymentMethod = paymentMethod,
				DiscountPercent = percent,
				IsActive = true
			});
			context.SaveChanges();
		}

		private static void AddProductWithCart(DbContextOptions<ApplicationDbContext> options,
			string name, decimal price, int count)
		{
			using var context = new ApplicationDbContext(options);
			var product = new Product
			{
				Name = name,
				Description = "Extra line for session proration tests.",
				MaxExpectation = 10,
				CategoryId = context.Categories.Select(c => c.Id).First(),
				ListPrice = price,
				FinalRetailPrice = price,
				FinalWholesalePrice = price - 30m,
				IsAvailableInStore = true,
				StockQuantity = 10,
			};
			context.Products.Add(product);
			context.ShoppingCarts.Add(new ShoppingCart
			{
				ApplicationUserId = "user-1", Product = product, Count = count
			});
			context.SaveChanges();
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
