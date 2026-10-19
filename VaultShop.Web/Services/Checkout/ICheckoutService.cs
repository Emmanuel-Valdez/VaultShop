using VaultShop.Models;
using VaultShop.Models.ViewModels;
using VaultShop.Web.Services.Payments;
using VaultShop.Web.Services.Pricing;

namespace VaultShop.Web.Services.Checkout
{
	public interface ICheckoutService
	{
		CheckoutSummaryResult BuildSummary(string userId, bool useWholesalePrice, string? couponCode = null, string? paymentMethod = null);
		CheckoutCreateOrderResult CreateOrder(string userId, OrderHeader postedOrderHeader, bool useWholesalePrice, string? couponCode = null);
		// descuento-medio-de-pago §1: session-only line items prorated to OrderTotal.
		IReadOnlyList<PaymentSessionLineItem> BuildPaymentSessionLineItems(ShoppingCartVM vm);

		// envio-gratis-o-cargo 3.1: single source for the cart/summary shipping amount —
		// hardens the free/pay split so display, summary totals and order creation agree.
		ShippingQuote GetShippingQuote(IEnumerable<ShoppingCart> carts, bool useWholesalePrice);

		// envio-gratis-o-cargo 4.2: modality availability for the selected locality and cart.
		// Gran Mendoza paid: M, V, S; Gran Mendoza free: M, S; elsewhere (and wholesale): S.
		IReadOnlyList<string> GetAllowedDeliveryTypes(string? locality, bool useWholesalePrice, ShippingQuote quote);

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
			// envio-gratis-o-cargo 3.1: cart/summary shipping display values.
			public ShippingQuote? Shipping { get; init; }
		}

		// envio-gratis-o-cargo 3.1: charged amount, free qualification, and the standard
		// (struck-through) amount shown when the cart ships free.
		public sealed record ShippingQuote(bool QualifiesForFreeShipping, decimal Charge, decimal StandardAmount);

		public sealed class CheckoutCreateOrderResult
		{
			public bool IsAuthorized { get; init; } = true;
			public bool IsCartEmpty { get; init; }
			public bool ShouldBlockUser { get; init; }
			public bool OrderTotalInvalid { get; init; }
			public bool InsufficientStock { get; init; }
			public bool VariantUnavailable { get; init; }
			// envio-gratis-o-cargo 4.2: posted modality is not offered for the cart/locality.
			public bool InvalidDeliveryModality { get; init; }
			// Coupon valid at apply time but invalid at creation: order proceeds undiscounted.
			public bool CouponDroppedAtCreation { get; init; }
			public int? OrderId { get; init; }
			public bool RequiresOnlinePayment { get; init; }
			public ShoppingCartVM? ShoppingCartVM { get; init; }
			public ApplicationUser? ApplicationUser { get; init; }
		}
	}
}
