## Why

VaultShop has no discount capability: every price is a fixed retail/wholesale value with no sale price, coupon, automatic promotion (2x1, 3x2), collection-scoped offer, or payment-method discount (e.g. bank-transfer -10%). This blocks standard 2026 storefront marketing mechanics and the Argentina-specific transfer incentive.

## What Changes

- **Direct offer price (product-level sale)**: optional sale price + date window per product, displayed as strikethrough list price; resolved before any other discount.
- **Coupon codes (cart-level)**: `% off` and fixed-amount coupons with code, usage limits, date window, minimum subtotal; one coupon per order; validated at apply time and re-validated at order creation.
- **Automatic quantity promotions (BxGy)**: buy-X-get-Y-free/discounted rules scoped to product, category/keyword (collection), or whole store; cheapest-unit-free evaluation; no code required.
- **Payment-method discount**: percentage discount for a payment method (starting with bank transfer, e.g. -10%); applied after line-level discounts; stackable with the single best specific discount.
- **Collection-scoped offers**: percentage-off rules scoped to a Keyword collection and/or Category, with date window.
- **Non-stacking rule for specific discounts**: at most ONE specific discount wins per line/order (offer vs. coupon vs. auto-promo vs. collection offer — best-price-wins); payment-method discount stacks on top.
- **Order traceability**: `OrderHeader` records discount breakdown (coupon code, promotion ids, discount totals); `OrderDetail` freezes the effective unit price plus discount motive.
- **Storefront communication**: sale/promotion badges on listings and details; cart/checkout line showing applied discount and motive.

## Capabilities

### New Capabilities
- `pricing/discounts`: direct offers, coupons, BxGy auto-promotions, collection offers, payment-method discount, stacking rules, order traceability, storefront badges.

### Modified Capabilities
- `pricing`: price resolution order changes — base role price, then best specific discount, then payment-method discount.
- `order-lifecycle`: order creation freezes discount breakdown; totals include discount lines.
- `catalog`: product/category/collection displays show sale and promotion badges.

## Impact

- Affected code: `PricingHelper`, `CartController`, `CheckoutService`, `OrderHeader/OrderDetail`, cart/checkout views, admin product/coupon/promotion management, transactional emails + PDF (discount lines).
- New models/migrations: `Coupon`, `Promotion` (+ rule fields), `OrderHeader` discount columns.
- No breaking API changes; existing fixed prices remain the fallback when no discount applies.
