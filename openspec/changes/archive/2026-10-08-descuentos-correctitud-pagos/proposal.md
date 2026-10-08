## Why

A backend audit of the shipped `descuentos-promociones` change verified real money bugs in the discount engine: multi-group BxGy under-grants (systematic overcharge), coupons degrade larger per-line discounts, and persisted line prices can disagree with the order total by rounding. Everything touching payments must be exactly correct — this change fixes all confirmed defects plus the spec gaps and test blind spots the audit exposed.

## What Changes

- BxGy grants every earned group per line (accumulate within a promotion, best-price-wins across promotions) instead of one group per cart line.
- Coupon application preserves larger per-line specific discounts (`Max` per line); header coupon total becomes the sum of winning per-line shares (rule documented explicitly).
- Order line prices round to cents with the remainder absorbed by the last line, so persisted lines always sum to the header total.
- `OrderHeader.PaymentDiscountMotive` becomes a persisted column (migration) instead of `[NotMapped]`; all four order surfaces render the frozen motive.
- Atomic coupon `UsesCount` increment via conditional `UPDATE` (same pattern as `DecrementStockIfSufficient`); over-use falls back to the existing undiscounted path.
- Storefront BxGy badge reuses the same grantability guards as the cart evaluator (active, window, wholesale opt-in, `BuyQty/GetQty > 0`, pct > 0) via one shared helper.
- Admin promotion form labels use explicit "take-X-pay-Y (group X+Y)" wording; canonical BxGy naming fixed in specs (`2x1 = Buy 1 Get 1`).
- Spec decision recorded: coupons apply to wholesale lines (code prevails, spec amended); no `Coupon.IncludeWholesale` flag.
- Regression tests for every fix: multi-group BxGy, coupon-vs-line preservation, lost-race coupon, frozen breakdown surviving deletion, badge/strikethrough rendering in both cultures, migration applies on scratch DB.

## Capabilities

### New Capabilities

- None — all behavior changes amend capabilities introduced by `descuentos-promociones`.

### Modified Capabilities

- `pricing/discounts`: BxGy multi-group grants; coupon per-line preservation + header-as-sum-of-winners rule; coupon applies to wholesale (decision); canonical BxGy naming.
- `pricing`: cart/checkout line-price rounding invariant (lines sum to header total).
- `order-lifecycle`: frozen payment-discount motive column; atomic coupon increment; rounding invariant on frozen lines.
- `catalog`: badge grantability parity (badge only when the cart would grant); explicit BxGy admin wording.

## Impact

- `VaultShop.Web/Services/Pricing/DiscountEvaluator.cs` (BxGy accumulation, coupon `Max`, shared `IsGrantableBxGy` helper).
- `VaultShop.Web/Services/Pricing/StorefrontPricingService.cs` (badge uses shared helper).
- `VaultShop.Web/Services/Checkout/CheckoutService.cs` (line-price rounding + remainder, atomic coupon increment).
- `VaultShop.Models/OrderHeader.cs` + EF migration (`PaymentDiscountMotive` column); `OrderSummaryService`/email/PDF mappings already pass the value through once persisted.
- `VaultShop.DataAccess/Repository` (+ `ICouponRepository.TryIncrementUses` conditional update).
- Admin promotion Upsert view labels + resx (wording only, no behavior change to valid configs).
- `VaultShop.Tests`: 8+ new tests (evaluator, checkout race, frozen breakdown, rendering, migration).
