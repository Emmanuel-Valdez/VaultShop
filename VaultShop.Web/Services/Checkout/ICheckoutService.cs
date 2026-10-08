using VaultShop.Models;
using VaultShop.Models.ViewModels;
using VaultShop.Web.Services.Pricing;

namespace VaultShop.Web.Services.Checkout
{
	public interface ICheckoutService
	{
		CheckoutSummaryResult BuildSummary(string userId, bool useWholesalePrice, string? couponCode = null, string? paymentMethod = null);
		CheckoutCreateOrderResult CreateOrder(string userId, OrderHeader postedOrderHeader, bool useWholesalePrice, string? couponCode = null);

		// descuentos-promociones: single funnel for cart display + summary + order
		// creation. Loads active coupons/promotions, evaluates, and stamps the
		// display fields (Price/OriginalPrice/DiscountAmount/DiscountMotive) on
		// each cart line. Keyed by cart id so variant-split lines map back.
		DiscountEvaluation EvaluateCart(IEnumerable<ShoppingCart> carts, bool useWholesalePrice, string? couponCode, string? paymentMethod);

		public sealed class CheckoutSummaryResult
		{
			public bool IsAuthorized { get; init; } = true;
			public bool IsCartEmpty { get; init; }
			public bool ShouldBlockUser { get; init; }
			public ShoppingCartVM? ShoppingCartVM { get; init; }
			public ApplicationUser? ApplicationUser { get; init; }
			public DiscountEvaluation? Discounts { get; init; }
		}

		public sealed class CheckoutCreateOrderResult
		{
			public bool IsAuthorized { get; init; } = true;
			public bool IsCartEmpty { get; init; }
			public bool ShouldBlockUser { get; init; }
			public bool OrderTotalInvalid { get; init; }
			public bool InsufficientStock { get; init; }
			public bool VariantUnavailable { get; init; }
			// Coupon valid at apply time but invalid at creation: order proceeds undiscounted.
			public bool CouponDroppedAtCreation { get; init; }
			public int? OrderId { get; init; }
			public bool RequiresOnlinePayment { get; init; }
			public ShoppingCartVM? ShoppingCartVM { get; init; }
			public ApplicationUser? ApplicationUser { get; init; }
		}
	}
}
