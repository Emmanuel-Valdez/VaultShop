using VaultShop.Models;

namespace VaultShop.Web.Services.Pricing;

// descuentos-promociones §2: base price → best specific → payment-method line.
// Best-price-wins: at most ONE specific discount (offer, coupon share, BxGy
// share, collection offer) applies per subtotal; the payment-method discount
// stacks on top. Wholesale lines skip Promotion-driven specifics unless the
// promotion opts in (IncludeWholesale); direct offers are per-list (a null
// SaleWholesalePrice means no wholesale offer); coupons are role-independent
// by design (Coupon carries no IncludeWholesale flag).
public class DiscountEvaluator : IDiscountEvaluator
{
	public const string OfferMotive = "Oferta";

	public DiscountEvaluation Evaluate(DiscountEvaluationRequest request)
	{
		var now = request.UtcNow;
		var lines = request.Lines.Where(l => l.Count > 0).ToList();
		var baseTotals = lines.Select(l => BaseUnit(l, request.UseWholesalePrice) * l.Count).ToList();
		var subtotalBase = baseTotals.Sum();

		var offerPayables = lines.Select(l => OfferPayable(l, request.UseWholesalePrice, now)).ToList();
		var (bxgyDiscounts, bxgyPromos) = EvaluateBxGy(lines, request, now);
		var (collectionDiscounts, collectionPromos) = EvaluateCollectionOff(lines, baseTotals, request, now);

		// Per-line specific winner (best-price-wins).
		var lineDiscounts = new decimal[lines.Count];
		var lineMotives = new string?[lines.Count];
		var linePromos = new int?[lines.Count];
		for (var i = 0; i < lines.Count; i++)
		{
			var offerDiscount = baseTotals[i] - offerPayables[i];
			var best = (Amount: 0m, Motive: (string?)null, PromoId: (int?)null);
			if (offerDiscount > 0 && offerDiscount > best.Amount)
				best = (offerDiscount, OfferMotive, null);
			if (bxgyDiscounts[i] > best.Amount)
				best = (bxgyDiscounts[i], bxgyPromos[i]!.Name, bxgyPromos[i]!.Id);
			if (collectionDiscounts[i] > best.Amount)
				best = (collectionDiscounts[i], collectionPromos[i]!.Name, collectionPromos[i]!.Id);
			lineDiscounts[i] = best.Amount;
			lineMotives[i] = best.Motive;
			linePromos[i] = best.PromoId;
		}

		// Coupon: validated now (apply time) and re-validated by re-evaluating at
		// order creation with fresh rows; an invalid coupon never blocks the order.
		var couponState = CouponEvaluationState.None;
		var rejection = CouponRejectionReason.Unknown;
		string? appliedCoupon = null;
		Coupon? coupon = null;
		decimal couponTotal = 0m;
		if (!string.IsNullOrWhiteSpace(request.CouponCode))
		{
			coupon = request.Coupons.FirstOrDefault(c =>
				string.Equals(c.Code, request.CouponCode!.Trim(), StringComparison.OrdinalIgnoreCase));
			rejection = ValidateCoupon(coupon, subtotalBase, now);
			if (rejection == CouponRejectionReason.Unknown && coupon is not null)
			{
				couponTotal = coupon.DiscountType == CouponDiscountType.Percent
					? subtotalBase * ClampPercent(coupon.Value) / 100m
					: Math.Min(coupon.Value, subtotalBase);
				couponTotal = Round(couponTotal);
				if (couponTotal > lineDiscounts.Sum() && couponTotal > 0)
				{
					couponState = CouponEvaluationState.Applied;
					appliedCoupon = coupon.Code;
					ProrateCoupon(lines, baseTotals, subtotalBase, couponTotal, coupon.Code, lineDiscounts, lineMotives, linePromos);
				}
				else
				{
					couponState = CouponEvaluationState.Superseded;
				}
			}
			else
			{
				couponState = CouponEvaluationState.Rejected;
			}
		}

		// Payment-method discount stacks on the post-specifics subtotal.
		var paymentDiscount = 0m;
		string? paymentMotive = null;
		var paymentPromoIds = new List<int>();
		if (!string.IsNullOrWhiteSpace(request.PaymentMethod))
		{
			var payPromo = request.Promotions
				.Where(p => p.Kind == PromotionKind.PaymentMethodDiscount
					&& p.IsActive
					&& InWindow(p.ValidFromUtc, p.ValidToUtc, now)
					&& string.Equals(p.PaymentMethod, request.PaymentMethod, StringComparison.OrdinalIgnoreCase))
				.OrderByDescending(p => p.DiscountPercent)
				.FirstOrDefault();
			if (payPromo is not null)
			{
				var payBase = subtotalBase - lineDiscounts.Sum();
				if (payBase > 0)
				{
					paymentDiscount = Round(payBase * ClampPercent(payPromo.DiscountPercent) / 100m);
					paymentMotive = payPromo.Name;
					paymentPromoIds.Add(payPromo.Id);
				}
			}
		}

		var evaluated = lines.Select((l, i) => new EvaluatedDiscountLine
		{
			LineKey = l.LineKey,
			ProductId = l.ProductId,
			Count = l.Count,
			BaseUnitPrice = BaseUnit(l, request.UseWholesalePrice),
			BaseTotal = baseTotals[i],
			DiscountAmount = lineDiscounts[i],
			DiscountMotive = lineMotives[i],
			PromotionId = linePromos[i]
		}).ToList();

		return new DiscountEvaluation
		{
			Lines = evaluated,
			SubtotalBase = subtotalBase,
			SpecificDiscountTotal = lineDiscounts.Sum(),
			PaymentDiscountTotal = paymentDiscount,
			PaymentDiscountMotive = paymentMotive,
			CouponState = couponState,
			CouponRejectionReason = rejection,
			AppliedCouponCode = appliedCoupon,
			AppliedPromotionIds = linePromos.Where(p => p.HasValue).Select(p => p!.Value)
				.Concat(paymentPromoIds).Distinct().ToList()
		};
	}

	private static decimal BaseUnit(DiscountLineRequest l, bool wholesale)
		=> wholesale ? l.WholesalePrice : l.RetailPrice;

	private static decimal OfferPayable(DiscountLineRequest l, bool wholesale, DateTime now)
	{
		var sale = wholesale ? l.SaleWholesalePrice : l.SaleRetailPrice;
		if (sale.HasValue && InWindow(l.SaleFromUtc, l.SaleToUtc, now))
			return sale.Value * l.Count;
		return BaseUnit(l, wholesale) * l.Count;
	}

	private static (decimal[] Discounts, Promotion?[] Promos) EvaluateBxGy(
		List<DiscountLineRequest> lines,
		DiscountEvaluationRequest request, DateTime now)
	{
		var discounts = new decimal[lines.Count];
		var promos = new Promotion?[lines.Count];
		foreach (var promo in request.Promotions.Where(p => p.Kind == PromotionKind.BxGy))
		{
			var groupSize = promo.BuyQty + promo.GetQty;
			var pct = ClampPercent(promo.GetDiscountPercent);
			if (pct <= 0) continue;
			var eligible = lines.Select((l, i) => (Line: l, Index: i))
				.Where(x => IsGrantableBxGy(promo, request.UseWholesalePrice, now, x.Line)).ToList();
			if (eligible.Count == 0) continue;
			// ponytail: accumulate per promotion (every earned group adds), max across promotions.
			var local = new decimal[lines.Count];
			// ponytail: one pool per product when SameSkuOnly, else a single mixed pool.
			foreach (var pool in promo.SameSkuOnly
				? eligible.GroupBy(x => x.Line.ProductId).Select(g => (IEnumerable<(DiscountLineRequest Line, int Index)>)g)
				: new[] { (IEnumerable<(DiscountLineRequest Line, int Index)>)eligible })
			{
				var units = pool.SelectMany(x => Enumerable.Repeat(
					(Index: x.Index, Price: BaseUnit(x.Line, request.UseWholesalePrice)), x.Line.Count))
					.OrderBy(u => u.Price).ToList();
				for (var g = 0; g + groupSize <= units.Count; g += groupSize)
					for (var k = 0; k < promo.GetQty; k++)
					{
						var u = units[g + k];
						local[u.Index] += Round(u.Price * pct / 100m);
					}
			}
			for (var i = 0; i < lines.Count; i++)
				if (local[i] > discounts[i])
				{
					discounts[i] = local[i];
					promos[i] = promo;
				}
		}
		return (discounts, promos);
	}

	internal static bool IsGrantableBxGy(Promotion promo, bool useWholesalePrice, DateTime now, DiscountLineRequest line)
		=> promo.Kind == PromotionKind.BxGy
			&& promo.IsActive
			&& InWindow(promo.ValidFromUtc, promo.ValidToUtc, now)
			&& (promo.IncludeWholesale || !useWholesalePrice)
			&& promo.BuyQty > 0
			&& promo.GetQty > 0
			&& ClampPercent(promo.GetDiscountPercent) > 0
			&& InScope(promo, line);

	private static (decimal[] Discounts, Promotion?[] Promos) EvaluateCollectionOff(
		List<DiscountLineRequest> lines, List<decimal> baseTotals,
		DiscountEvaluationRequest request, DateTime now)
	{
		var discounts = new decimal[lines.Count];
		var promos = new Promotion?[lines.Count];
		var offers = request.Promotions.Where(p =>
			p.Kind == PromotionKind.PercentOff && p.IsActive
			&& InWindow(p.ValidFromUtc, p.ValidToUtc, now)
			&& (p.IncludeWholesale || !request.UseWholesalePrice)).ToList();
		for (var i = 0; i < lines.Count; i++)
		{
			foreach (var promo in offers.Where(p => InScope(p, lines[i])))
			{
				var d = Round(baseTotals[i] * ClampPercent(promo.DiscountPercent) / 100m);
				if (d > discounts[i])
				{
					discounts[i] = d;
					promos[i] = promo;
				}
			}
		}
		return (discounts, promos);
	}

	private static bool InScope(Promotion p, DiscountLineRequest l)
		=> p.Scope switch
		{
			PromotionScope.Store => true,
			PromotionScope.Product => p.ProductId.HasValue && p.ProductId.Value == l.ProductId,
			PromotionScope.Category => p.CategoryId.HasValue && p.CategoryId.Value == l.CategoryId,
			PromotionScope.Keyword => p.KeywordId.HasValue && l.KeywordIds.Contains(p.KeywordId.Value),
			_ => false
		};

	private static CouponRejectionReason ValidateCoupon(Coupon? coupon, decimal subtotalBase, DateTime now)
	{
		if (coupon is null) return CouponRejectionReason.NotFound;
		if (!coupon.IsActive) return CouponRejectionReason.Inactive;
		if (!InWindow(coupon.ValidFromUtc, coupon.ValidToUtc, now)) return CouponRejectionReason.Expired;
		if (coupon.MaxUses.HasValue && coupon.UsesCount >= coupon.MaxUses.Value)
			return CouponRejectionReason.MaxUsesReached;
		if ((coupon.MinSubtotal ?? 0m) > subtotalBase) return CouponRejectionReason.BelowMinimumSubtotal;
		return CouponRejectionReason.Unknown;
	}

	private static void ProrateCoupon(List<DiscountLineRequest> lines, List<decimal> baseTotals,
		decimal subtotalBase, decimal couponTotal, string code,
		decimal[] lineDiscounts, string?[] lineMotives, int?[] linePromos)
	{
		if (subtotalBase <= 0) return;
		var motive = $"Cupón {code}";
		// ponytail: nominal shares sum exactly (remainder on largest); each line keeps Max(existing, share).
		var largest = 0;
		for (var i = 1; i < lines.Count; i++)
			if (baseTotals[i] > baseTotals[largest]) largest = i;
		var shares = new decimal[lines.Count];
		var assigned = 0m;
		for (var i = 0; i < lines.Count; i++)
		{
			if (i == largest) continue;
			shares[i] = Round(couponTotal * baseTotals[i] / subtotalBase);
			assigned += shares[i];
		}
		shares[largest] = couponTotal - assigned;
		for (var i = 0; i < lines.Count; i++)
		{
			if (shares[i] > lineDiscounts[i])
			{
				lineDiscounts[i] = shares[i];
				lineMotives[i] = motive;
				linePromos[i] = null;
			}
		}
	}

	private static bool InWindow(DateTime? from, DateTime? to, DateTime now)
		=> (!from.HasValue || now >= from.Value) && (!to.HasValue || now <= to.Value);

	private static decimal ClampPercent(decimal pct) => Math.Clamp(pct, 0m, 100m);

	private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
