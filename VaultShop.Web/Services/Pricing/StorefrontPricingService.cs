using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;

namespace VaultShop.Web.Services.Pricing;

// descuentos-promociones §4: storefront display runs the same evaluator as
// cart/checkout (design.md decision 1) in display mode — one unit, no coupon,
// no payment method — so badges can never promise more than the cart grants.
// BxGy needs several units to grant, so its badge is driven by coverage
// (an active rule matching the viewer), not by a granted amount.
public interface IStorefrontPricingService
{
	IReadOnlyDictionary<int, ProductDisplayPrice> GetDisplayPrices(IEnumerable<Product> products, bool useWholesale);
}

public sealed record ProductDisplayPrice(decimal BasePrice, decimal EffectivePrice, string? Motive, DateTime? OfferEndUtc = null)
{
	public bool HasDiscount => EffectivePrice < BasePrice;
	public bool HasBadge => !string.IsNullOrWhiteSpace(Motive);
}

public static class DiscountMotives
{
	// ponytail: evaluator motives are stored Spanish ("Oferta", "Cupón X");
	// views pass their localized labels in — promotion names pass through as-is.
	public static string? Badge(string? motive, string offerLabel, string couponLabel)
	{
		if (string.IsNullOrWhiteSpace(motive)) return null;
		if (string.Equals(motive, DiscountEvaluator.OfferMotive, StringComparison.Ordinal))
			return offerLabel;
		const string couponPrefix = "Cupón ";
		if (motive.StartsWith(couponPrefix, StringComparison.Ordinal))
			return couponLabel + motive.Substring(couponPrefix.Length);
		return motive;
	}
}

public class StorefrontPricingService : IStorefrontPricingService
{
	private readonly IUnitOfWork _unitOfWork;
	private readonly IDiscountEvaluator _discounts;

	public StorefrontPricingService(IUnitOfWork unitOfWork, IDiscountEvaluator discounts)
	{
		_unitOfWork = unitOfWork;
		_discounts = discounts;
	}

	public IReadOnlyDictionary<int, ProductDisplayPrice> GetDisplayPrices(IEnumerable<Product> products, bool useWholesale)
	{
		var list = products.ToList();
		var result = new Dictionary<int, ProductDisplayPrice>(list.Count);
		if (list.Count == 0) return result;
		var now = DateTime.UtcNow;
		var promotions = _unitOfWork.Promotion.GetAll(p => p.IsActive).ToList();
		var evaluation = _discounts.Evaluate(new DiscountEvaluationRequest
		{
			Lines = list.Select(p => new DiscountLineRequest
			{
				LineKey = p.Id,
				ProductId = p.Id,
				CategoryId = p.CategoryId,
				KeywordIds = p.Keywords?.Select(k => k.KeywordId).ToList() ?? new List<int>(),
				Count = 1,
				RetailPrice = p.FinalRetailPrice,
				WholesalePrice = p.FinalWholesalePrice,
				SaleRetailPrice = p.SaleRetailPrice,
				SaleWholesalePrice = p.SaleWholesalePrice,
				SaleFromUtc = p.SaleFromUtc,
				SaleToUtc = p.SaleToUtc
			}).ToList(),
			UseWholesalePrice = useWholesale,
			Coupons = Array.Empty<Coupon>(),
			Promotions = promotions,
			UtcNow = now
		});
		// ponytail: keyed by product id (one line per product); first-wins tolerates dupes.
		var byProduct = evaluation.Lines.GroupBy(l => l.ProductId).ToDictionary(g => g.Key, g => g.First());
		foreach (var p in list)
		{
			if (!byProduct.TryGetValue(p.Id, out var line))
			{
				var fallback = useWholesale ? p.FinalWholesalePrice : p.FinalRetailPrice;
				result[p.Id] = new ProductDisplayPrice(fallback, fallback, null);
				continue;
			}
		var motive = line.DiscountAmount > 0
				? line.DiscountMotive
				: FindCoveringBxGy(promotions, p, useWholesale, now)?.Name;
			// ponytail: surface the sale deadline only for the direct product offer;
			// BxGy / promotion end dates arrive when the evaluator can attribute them.
			var offerEnd = motive == DiscountEvaluator.OfferMotive ? p.SaleToUtc : (DateTime?)null;
			result[p.Id] = new ProductDisplayPrice(line.BaseUnitPrice, line.EffectiveTotal, motive, offerEnd);
		}
		return result;
	}

	private static Promotion? FindCoveringBxGy(List<Promotion> promotions, Product p, bool useWholesale, DateTime now)
		=> promotions.FirstOrDefault(promo => DiscountEvaluator.IsGrantableBxGy(promo, useWholesale, now, new DiscountLineRequest
		{
			ProductId = p.Id,
			CategoryId = p.CategoryId,
			KeywordIds = p.Keywords?.Select(k => k.KeywordId).ToList() ?? new List<int>()
		}));
}
