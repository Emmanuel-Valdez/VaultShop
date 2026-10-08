using VaultShop.Models;

namespace VaultShop.Web.Services.Pricing;

// descuentos-promociones §2: single shared discount evaluator used by storefront
// display, cart, and checkout. Pure function over the passed-in coupons and
// promotions — callers load active rows, tests pass plain objects.
public interface IDiscountEvaluator
{
	DiscountEvaluation Evaluate(DiscountEvaluationRequest request);
}

public sealed class DiscountLineRequest
{
	// Caller-owned key (e.g. cart line id) echoed back for variant-split mapping.
	public int LineKey { get; init; }
	public int ProductId { get; init; }
	public int CategoryId { get; init; }
	public IReadOnlyList<int> KeywordIds { get; init; } = Array.Empty<int>();
	public int Count { get; init; }
	public decimal RetailPrice { get; init; }
	public decimal WholesalePrice { get; init; }
	public decimal? SaleRetailPrice { get; init; }
	public decimal? SaleWholesalePrice { get; init; }
	public DateTime? SaleFromUtc { get; init; }
	public DateTime? SaleToUtc { get; init; }
}

public sealed class DiscountEvaluationRequest
{
	public IReadOnlyList<DiscountLineRequest> Lines { get; init; } = Array.Empty<DiscountLineRequest>();
	public bool UseWholesalePrice { get; init; }
	public string? CouponCode { get; init; }
	public IReadOnlyList<Coupon> Coupons { get; init; } = Array.Empty<Coupon>();
	public IReadOnlyList<Promotion> Promotions { get; init; } = Array.Empty<Promotion>();
	public string? PaymentMethod { get; init; }
	public DateTime UtcNow { get; init; } = DateTime.UtcNow;
}

public enum CouponEvaluationState
{
	// No coupon code was supplied.
	None = 0,
	// Coupon applied (won best-price-wins).
	Applied = 1,
	// Code supplied but invalid (see CouponRejectionReason).
	Rejected = 2,
	// Valid coupon, but line-level discounts yield a lower payable.
	Superseded = 3
}

public enum CouponRejectionReason
{
	Unknown = 0,
	NotFound = 1,
	Inactive = 2,
	Expired = 3,
	MaxUsesReached = 4,
	BelowMinimumSubtotal = 5
}

public sealed class EvaluatedDiscountLine
{
	public int LineKey { get; init; }
	public int ProductId { get; init; }
	public int Count { get; init; }
	public decimal BaseUnitPrice { get; init; }
	public decimal BaseTotal { get; init; }
	public decimal DiscountAmount { get; init; }
	public string? DiscountMotive { get; init; }
	public int? PromotionId { get; init; }
	public decimal EffectiveTotal => BaseTotal - DiscountAmount;
}

public sealed class DiscountEvaluation
{
	public IReadOnlyList<EvaluatedDiscountLine> Lines { get; init; } = Array.Empty<EvaluatedDiscountLine>();
	public decimal SubtotalBase { get; init; }
	public decimal SpecificDiscountTotal { get; init; }
	public decimal PaymentDiscountTotal { get; init; }
	public string? PaymentDiscountMotive { get; init; }
	public decimal Total => SubtotalBase - SpecificDiscountTotal - PaymentDiscountTotal;
	public CouponEvaluationState CouponState { get; init; } = CouponEvaluationState.None;
	public CouponRejectionReason CouponRejectionReason { get; init; }
	public string? AppliedCouponCode { get; init; }
	public IReadOnlyList<int> AppliedPromotionIds { get; init; } = Array.Empty<int>();
}
