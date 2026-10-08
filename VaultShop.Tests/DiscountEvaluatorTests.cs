using VaultShop.Models;
using VaultShop.Web.Services.Pricing;

namespace VaultShop.Web.Tests
{
	// descuentos-promociones §2: one shared evaluator — offer, coupon, BxGy,
	// collection %, payment-method stacking, best-price-wins, wholesale gate.
	public class DiscountEvaluatorTests
	{
		private static readonly DateTime Now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
		private readonly DiscountEvaluator _sut = new();

		private static DiscountLineRequest Line(int productId, decimal price, int count = 1,
			int categoryId = 1, int[]? keywords = null,
			decimal? saleRetail = null, decimal? saleWholesale = null,
			DateTime? saleFrom = null, DateTime? saleTo = null) => new()
			{
				ProductId = productId,
				CategoryId = categoryId,
				KeywordIds = keywords ?? Array.Empty<int>(),
				Count = count,
				RetailPrice = price,
				WholesalePrice = price - 30m,
				SaleRetailPrice = saleRetail,
				SaleWholesalePrice = saleWholesale,
				SaleFromUtc = saleFrom,
				SaleToUtc = saleTo
			};

		private DiscountEvaluation Eval(IEnumerable<DiscountLineRequest> lines,
			bool wholesale = false, string? coupon = null, Coupon[]? coupons = null,
			Promotion[]? promos = null, string? paymentMethod = null, DateTime? now = null) =>
			_sut.Evaluate(new DiscountEvaluationRequest
			{
				Lines = lines.ToList(),
				UseWholesalePrice = wholesale,
				CouponCode = coupon,
				Coupons = coupons ?? Array.Empty<Coupon>(),
				Promotions = promos ?? Array.Empty<Promotion>(),
				PaymentMethod = paymentMethod,
				UtcNow = now ?? Now
			});

		private static Coupon PercentCoupon(string code = "BIENVENIDA10", decimal value = 10m,
			decimal? min = 20000m, DateTime? from = null, DateTime? to = null,
			int? maxUses = null, int uses = 0, bool active = true) => new()
			{
				Code = code, DiscountType = CouponDiscountType.Percent, Value = value,
				MinSubtotal = min, ValidFromUtc = from, ValidToUtc = to,
				MaxUses = maxUses, UsesCount = uses, IsActive = active
			};

		private static Promotion BxGy(string name = "2x1", int buy = 1, int get = 1,
			decimal getPct = 100m, PromotionScope scope = PromotionScope.Store,
			int? productId = null, int? categoryId = null, int? keywordId = null,
			bool sameSku = false, bool wholesale = false,
			DateTime? from = null, DateTime? to = null) => new()
			{
				Name = name, Kind = PromotionKind.BxGy, Scope = scope,
				ProductId = productId, CategoryId = categoryId, KeywordId = keywordId,
				BuyQty = buy, GetQty = get, GetDiscountPercent = getPct,
				SameSkuOnly = sameSku, IncludeWholesale = wholesale,
				ValidFromUtc = from, ValidToUtc = to
			};

		// ---- direct offer ----

		[Fact]
		public void ActiveRetailOffer_AppliesWithStrikethroughData()
		{
			var r = Eval(new[] { Line(1, 10000m, saleRetail: 8000m,
				saleFrom: Now.AddDays(-1), saleTo: Now.AddDays(1)) });

			var line = Assert.Single(r.Lines);
			Assert.Equal(10000m, line.BaseTotal);
			Assert.Equal(2000m, line.DiscountAmount);
			Assert.Equal(DiscountEvaluator.OfferMotive, line.DiscountMotive);
			Assert.Equal(8000m, line.EffectiveTotal);
			Assert.Equal(8000m, r.Total);
		}

		[Fact]
		public void ExpiredOffer_Ignored()
		{
			var r = Eval(new[] { Line(1, 10000m, saleRetail: 8000m, saleTo: Now.AddDays(-1)) });

			Assert.Equal(0m, r.SpecificDiscountTotal);
			Assert.Null(Assert.Single(r.Lines).DiscountMotive);
		}

		[Fact]
		public void NoOffer_NoBadge()
		{
			var r = Eval(new[] { Line(1, 10000m) });

			Assert.Equal(0m, r.SpecificDiscountTotal);
			Assert.Equal(10000m, r.Total);
		}

		// ---- coupons ----

		[Fact]
		public void PercentageCoupon_AppliesDiscountLine()
		{
			var r = Eval(new[] { Line(1, 50000m) },
				coupon: "bienvenida10", coupons: new[] { PercentCoupon() });

			Assert.Equal(CouponEvaluationState.Applied, r.CouponState);
			Assert.Equal("BIENVENIDA10", r.AppliedCouponCode);
			Assert.Equal(5000m, r.SpecificDiscountTotal);
			Assert.Equal(45000m, r.Total);
			Assert.Equal("Cupón BIENVENIDA10", Assert.Single(r.Lines).DiscountMotive);
		}

		[Fact]
		public void FixedCoupon_CappedAtSubtotal()
		{
			var r = Eval(new[] { Line(1, 10000m) },
				coupon: "BIG", coupons: new[]
				{
					new Coupon { Code = "BIG", DiscountType = CouponDiscountType.FixedAmount,
						Value = 15000m, IsActive = true }
				});

			Assert.Equal(CouponEvaluationState.Applied, r.CouponState);
			Assert.Equal(10000m, r.SpecificDiscountTotal);
			Assert.Equal(0m, r.Total);
		}

		[Fact]
		public void CouponBelowMinimum_Rejected()
		{
			var r = Eval(new[] { Line(1, 10000m) },
				coupon: "BIENVENIDA10", coupons: new[] { PercentCoupon() });

			Assert.Equal(CouponEvaluationState.Rejected, r.CouponState);
			Assert.Equal(CouponRejectionReason.BelowMinimumSubtotal, r.CouponRejectionReason);
			Assert.Equal(0m, r.SpecificDiscountTotal);
		}

		[Theory]
		[InlineData(false, null, null, null, 0, CouponRejectionReason.Inactive)]
		[InlineData(true, -2, -1, null, 0, CouponRejectionReason.Expired)]
		[InlineData(true, null, null, 1, 1, CouponRejectionReason.MaxUsesReached)]
		public void InvalidCoupon_RejectedWithReason(bool active, int? toOffsetDays,
			int? fromOffsetDays, int? maxUses, int uses, CouponRejectionReason expected)
		{
			var coupon = PercentCoupon(min: null, active: active, maxUses: maxUses, uses: uses,
				from: fromOffsetDays.HasValue ? Now.AddDays(fromOffsetDays.Value) : null,
				to: toOffsetDays.HasValue ? Now.AddDays(toOffsetDays.Value) : null);
			var r = Eval(new[] { Line(1, 50000m) }, coupon: "BIENVENIDA10", coupons: new[] { coupon });

			Assert.Equal(CouponEvaluationState.Rejected, r.CouponState);
			Assert.Equal(expected, r.CouponRejectionReason);
			Assert.Equal(50000m, r.Total);
		}

		[Fact]
		public void UnknownCouponCode_RejectedNotFound()
		{
			var r = Eval(new[] { Line(1, 50000m) }, coupon: "NOPE", coupons: new[] { PercentCoupon() });

			Assert.Equal(CouponEvaluationState.Rejected, r.CouponState);
			Assert.Equal(CouponRejectionReason.NotFound, r.CouponRejectionReason);
		}

		// ---- BxGy ----

		[Fact]
		public void BxGy2x1_GrantsCheapestUnitFree()
		{
			var r = Eval(new[]
			{
				Line(1, 100m, count: 2), Line(2, 60m)
			}, promos: new[] { BxGy() });

			Assert.Equal(60m, r.SpecificDiscountTotal);
			Assert.Equal("2x1", r.Lines.Single(l => l.ProductId == 2).DiscountMotive);
			Assert.Equal(0m, r.Lines.Single(l => l.ProductId == 1).DiscountAmount);
		}

		[Fact]
		public void BxGy3x2_GrantsOneFreePerThree()
		{
			var r = Eval(new[] { Line(1, 100m, count: 3) },
				promos: new[] { BxGy("3x2", buy: 2, get: 1) });

			Assert.Equal(100m, r.SpecificDiscountTotal);
			Assert.Equal(200m, r.Total);
		}

		[Fact]
		public void SecondUnitHalfPrice_GrantsFifty()
		{
			var r = Eval(new[] { Line(1, 100m, count: 2) },
				promos: new[] { BxGy("2da 50%", buy: 1, get: 1, getPct: 50m) });

			Assert.Equal(50m, r.SpecificDiscountTotal);
		}

		[Fact]
		public void OutOfWindowPromotion_Ignored()
		{
			var r = Eval(new[] { Line(1, 100m, count: 3) },
				promos: new[] { BxGy(to: Now.AddDays(-1)) });

			Assert.Equal(0m, r.SpecificDiscountTotal);
		}

		[Fact]
		public void InvalidBxGyPromotion_GrantsNothing()
		{
			var inactive = BxGy();
			inactive.IsActive = false;
			foreach (var promotion in new[]
			{
				inactive,
				BxGy(buy: 0),
				BxGy(get: 0),
				BxGy(getPct: 0m)
			})
			{
				var result = Eval(new[] { Line(1, 100m, count: 2) }, promos: new[] { promotion });
				Assert.Equal(0m, result.SpecificDiscountTotal);
				Assert.Null(Assert.Single(result.Lines).DiscountMotive);
			}
		}

		[Fact]
		public void SameSkuOnly_DoesNotPoolAcrossProducts()
		{
			var r = Eval(new[] { Line(1, 100m), Line(2, 60m) },
				promos: new[] { BxGy(sameSku: true) });

			Assert.Equal(0m, r.SpecificDiscountTotal);
		}

		// ---- collection offers ----

		[Fact]
		public void CollectionOffer_AppliesToMemberOnly()
		{
			var promo = new Promotion
			{
				Name = "Colección -20%", Kind = PromotionKind.PercentOff,
				Scope = PromotionScope.Keyword, KeywordId = 7, DiscountPercent = 20m, IsActive = true
			};
			var r = Eval(new[]
			{
				Line(1, 10000m, keywords: new[] { 7 }), Line(2, 10000m, keywords: new[] { 9 })
			}, promos: new[] { promo });

			Assert.Equal(2000m, r.SpecificDiscountTotal);
			Assert.Equal("Colección -20%", r.Lines.Single(l => l.ProductId == 1).DiscountMotive);
			Assert.Null(r.Lines.Single(l => l.ProductId == 2).DiscountMotive);
		}

		// ---- payment-method discount ----

		[Fact]
		public void TransferDiscount_StacksOnSalePrice()
		{
			var pay = new Promotion
			{
				Name = "Transferencia -10%", Kind = PromotionKind.PaymentMethodDiscount,
				Scope = PromotionScope.Store, PaymentMethod = "BankTransfer",
				DiscountPercent = 10m, IsActive = true
			};
			var r = Eval(new[] { Line(1, 10000m, saleRetail: 8000m,
					saleFrom: Now.AddDays(-1), saleTo: Now.AddDays(1)) },
				promos: new[] { pay }, paymentMethod: "BankTransfer");

			Assert.Equal(2000m, r.SpecificDiscountTotal);
			Assert.Equal(800m, r.PaymentDiscountTotal);
			Assert.Equal("Transferencia -10%", r.PaymentDiscountMotive);
			Assert.Equal(7200m, r.Total);
		}

		[Fact]
		public void MethodWithoutDiscount_AddsNothing()
		{
			var r = Eval(new[] { Line(1, 10000m) }, paymentMethod: "Stripe");

			Assert.Equal(0m, r.PaymentDiscountTotal);
			Assert.Null(r.PaymentDiscountMotive);
		}

		// ---- best-price-wins + wholesale + re-validation (2.2) ----

		[Fact]
		public void BestSpecificDiscount_Wins()
		{
			var collection = new Promotion
			{
				Name = "Colección -25%", Kind = PromotionKind.PercentOff,
				Scope = PromotionScope.Store, DiscountPercent = 25m, IsActive = true
			};
			var r = Eval(new[] { Line(1, 100m, saleRetail: 80m,
					saleFrom: Now.AddDays(-1), saleTo: Now.AddDays(1)) },
				promos: new[] { collection });

			Assert.Equal(25m, r.SpecificDiscountTotal);
			Assert.Equal("Colección -25%", Assert.Single(r.Lines).DiscountMotive);
		}

		[Fact]
		public void CouponAndAutoPromo_NeverCombine_LowerPayableWins()
		{
			var bxgy = BxGy();
			// 10% coupon on 100000 (10000 nominal); P0.1+P0.2: the mixed pool
			// grants two groups (200 on the cheap line) and the coupon share never
			// shrinks it — header is the sum of winners (200 + 9970).
			var withCoupon = Eval(new[] { Line(1, 100m, count: 3), Line(2, 99700m) },
				coupon: "BIENVENIDA10", coupons: new[] { PercentCoupon(min: null) },
				promos: new[] { bxgy });
			Assert.Equal(CouponEvaluationState.Applied, withCoupon.CouponState);
			Assert.Equal(10170m, withCoupon.SpecificDiscountTotal);
			Assert.Equal("2x1", withCoupon.Lines.Single(l => l.ProductId == 1).DiscountMotive);

			// Same cart without coupon keeps the BxGy benefit alone.
			var withoutCoupon = Eval(new[] { Line(1, 100m, count: 3), Line(2, 99700m) },
				promos: new[] { bxgy });
			Assert.Equal(200m, withoutCoupon.SpecificDiscountTotal);
		}

		[Fact]
		public void ValidCoupon_LosesToBetterLineDiscount_Superseded()
		{
			var r = Eval(new[] { Line(1, 100m, count: 3) },
				coupon: "BIENVENIDA10", coupons: new[] { PercentCoupon(min: null) },
				promos: new[] { BxGy() });

			Assert.Equal(CouponEvaluationState.Superseded, r.CouponState);
			Assert.Equal(100m, r.SpecificDiscountTotal);
			Assert.Equal("2x1", Assert.Single(r.Lines).DiscountMotive);
		}

		[Fact]
		public void WholesaleViewer_RetailOnlyPromotion_Skipped()
		{
			var r = Eval(new[] { Line(1, 100m, count: 3) }, wholesale: true, promos: new[] { BxGy() });

			Assert.Equal(0m, r.SpecificDiscountTotal);
			Assert.Equal(3 * 70m, r.Total);
		}

		[Fact]
		public void WholesaleOptInPromotion_Applies()
		{
			var r = Eval(new[] { Line(1, 100m, count: 3) },
				wholesale: true, promos: new[] { BxGy(wholesale: true) });

			Assert.Equal(70m, r.SpecificDiscountTotal);
		}

		[Fact]
		public void WholesaleOffer_UsesWholesaleSalePrice()
		{
			var r = Eval(new[] { Line(1, 100m, saleRetail: 80m, saleWholesale: 60m,
					saleFrom: Now.AddDays(-1), saleTo: Now.AddDays(1)) }, wholesale: true);

			Assert.Equal(10m, Assert.Single(r.Lines).DiscountAmount);
		}

		[Fact]
		public void BxGy_MultiQuantitySameSku_GrantsDiscountPerGroup()
		{
			// P0.1: 6 units @100, 2x1 (buy 1 get 1) → three free units = 300.
			var r = Eval(new[] { Line(1, 100m, count: 6) },
				promos: new[] { BxGy() });

			Assert.Equal(300m, r.SpecificDiscountTotal);
			Assert.Equal(300m, r.Total);
		}

		[Fact]
		public void Coupon_DoesNotShrinkALargerPerLineDiscount()
		{
			// P0.2: offer-5000 line keeps its larger discount; coupon only wins the flat line.
			var r = Eval(new[]
			{
				Line(1, 10000m, saleRetail: 5000m, saleFrom: Now.AddDays(-1), saleTo: Now.AddDays(1)),
				Line(2, 50000m)
			}, coupon: "BIENVENIDA10", coupons: new[] { PercentCoupon(min: null) });

			Assert.Equal(CouponEvaluationState.Applied, r.CouponState);
			var first = r.Lines.Single(l => l.ProductId == 1);
			var second = r.Lines.Single(l => l.ProductId == 2);
			Assert.Equal(5000m, first.DiscountAmount);
			Assert.Equal(DiscountEvaluator.OfferMotive, first.DiscountMotive);
			Assert.Equal(5000m, second.DiscountAmount);
			Assert.Equal("Cupón BIENVENIDA10", second.DiscountMotive);
			Assert.Equal(10000m, r.SpecificDiscountTotal);
		}

		[Fact]
		public void CouponExpiredAtCreation_OrderProceedsUndiscounted()
		{
			var coupon = PercentCoupon(min: null, to: Now.AddHours(1));
			var atApply = Eval(new[] { Line(1, 50000m) },
				coupon: "BIENVENIDA10", coupons: new[] { coupon }, now: Now);
			Assert.Equal(CouponEvaluationState.Applied, atApply.CouponState);

			// Re-validated at creation with fresh rows: expired → undiscounted, order not blocked.
			var atCreation = Eval(new[] { Line(1, 50000m) },
				coupon: "BIENVENIDA10", coupons: new[] { coupon }, now: Now.AddHours(2));
			Assert.Equal(CouponEvaluationState.Rejected, atCreation.CouponState);
			Assert.Equal(CouponRejectionReason.Expired, atCreation.CouponRejectionReason);
			Assert.Equal(50000m, atCreation.Total);
		}
	}
}
