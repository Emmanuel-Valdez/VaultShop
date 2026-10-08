## Context

VaultShop resolves a single role-based price (`PricingHelper.GetPrice` → `FinalRetailPrice` / `FinalWholesalePrice`), recomputed per request in `CartController` and `CheckoutService.GetPrice`, frozen as `OrderDetail.Price`. No coupon, offer, BxGy, collection-offer, or payment-discount concepts exist. See proposal.md for motivation.

## Goals / Non-Goals

**Goals:**
- One shared discount evaluator used by storefront display, cart, and checkout (single source of truth).
- Best-price-wins for specific discounts; payment-method discount stacks on top.
- Full order traceability (motives + amounts frozen).

**Non-Goals:**
- Stacking multiple specific discounts on the same line; loyalty points; gift cards; shipping-cost-as-line changes.
- Automatic wholesale inclusion — wholesale stays excluded unless a promotion opts in.
- AFIP/ARCA invoicing impact (discounts are commercial lines only).

## Decisions

- **Single `IDiscountEvaluator` service** over per-controller logic: takes (cart lines, user/role, coupon code, payment method, date) → returns per-line effective price + discount lines with motives. Alternative (discount code scattered in CartController + CheckoutService) rejected — the badge-vs-cart mismatch is the #1 conversion bug.
- **Specific discounts as competing candidates, not additive**: evaluator computes each eligible specific benefit per line/subtotal and keeps the minimum payable. Alternative (priority-ordered stacking) rejected per user rule — no stacking of specifics.
- **Payment-method discount as order-level line after specifics**: transfer -10% applies to the already-discounted eligible subtotal. Stored as `OrderHeader.PaymentDiscountTotal` + `PaymentMethod` snapshot so changing method before paying recomputes cleanly.
- **Coupon = one entity, % or fixed, cart-scoped**: `Coupon { Code, Type, Value, MinSubtotal, From/To, MaxUses, UsesCount, IsActive }`. Fixed capped at subtotal (never negative). Re-validated at order creation; failure = order proceeds undiscounted + notice.
- **BxGy = `Promotion { Kind=BxGy, BuyQty, GetQty, GetDiscountPct, Scope (product/category/keyword/store), SameSkuOnly?, IncludeWholesale?, From/To, IsActive }`**: cheapest-eligible-unit-first. Collection offers reuse `Promotion { Kind=PercentOff, Scope }`; direct offers are product columns (`SaleRetailPrice, SaleWholesalePrice, SaleFrom/To`) to keep the common case to zero joins.
- **Frozen trace**: `OrderDetail` gains `OriginalPrice, DiscountAmount, DiscountMotive`; `OrderHeader` gains `CouponCode, DiscountTotal, PaymentDiscountTotal, AppliedPromotionIds`. Past orders never recompute.
- **Wholesale exclusion default**: `IncludeWholesale=false` default on promotions; Company/preview-wholesale lines skip specific discounts unless opted in.

## Risks / Trade-offs

- [Risk] Badge says 2x1 but cart grants less (scope/wholesale mismatch) → Mitigation: badge uses the same evaluator in display mode.
- [Risk] Coupon race (two shoppers exhaust last use) → Mitigation: atomic increment guarded at order creation; over-use fails safe to undiscounted.
- [Risk] Transfer discount + bank-transfer Pending flow confusion (discount shown before admin approval) → Mitigation: payment discount frozen by *chosen* method at creation; method change requires order edit path.
- [Risk] Margin erosion from BxGy on low-margin artisan pieces → Mitigation: promotions are explicit opt-in per product/scope; admin list shows margin hint from `PricingCalculatorService`.
- [Trade-off] Best-price-wins can surprise ("why didn't my coupon apply?") → cart shows which discount won and why the other lost (one line of motive text).
