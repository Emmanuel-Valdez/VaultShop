## 1. Data model and migrations

- [ ] 1.1 Add Product sale columns (SaleRetailPrice, SaleWholesalePrice, SaleFrom/To) + migration, verify `dotnet ef migrations script` shows nullable columns with no data loss.
- [ ] 1.2 Add Coupon + Promotion entities, DbSets, and OrderHeader/OrderDetail discount columns + migration, verify migration applies on a scratch database.
- [ ] 1.3 Seed no active discounts by default and verify existing orders read back with zero discount totals.

## 2. Discount evaluator service

- [ ] 2.1 Implement IDiscountEvaluator (base price → best specific → payment-method line) with unit tests for each mechanic (offer, coupon %, coupon fixed + cap, BxGy 2x1/3x2/2nd-50%, collection %, transfer stacking), verify new xUnit suite green.
- [ ] 2.2 Implement best-price-wins + wholesale-excluded-by-default + coupon re-validation rule, verify unit tests cover winner selection, wholesale skip, and expired-at-creation fallback.

## 3. Cart and checkout wiring

- [ ] 3.1 Wire evaluator into CartController + cart summary view (discount lines + motives + coupon apply/remove), verify manual cart shows correct lines for offer, coupon, and 2x1 cases.
- [ ] 3.2 Wire evaluator into CheckoutService order creation (freeze OriginalPrice/DiscountAmount/Motive per line + header totals + coupon use increment), verify integration test creates a discounted order with frozen breakdown.
- [ ] 3.3 Apply payment-method discount from chosen method and recompute on method change, verify transfer order shows -10% line and card order shows none.

## 4. Storefront display

- [ ] 4.1 Render badges + strikethrough on Home/Search/Favorites/Details from evaluator display mode, verify badges match cart grants for the same viewer.
- [ ] 4.2 Localize badges, motives, and coupon errors (es-AR + en-US resx), verify both cultures render without missing-key fallback.

## 5. Admin management

- [ ] 5.1 Admin CRUD for product offers (dates + per-list prices) with validation, verify offer saves and activates only in window.
- [ ] 5.2 Admin CRUD for coupons and promotions (scope pickers, BxGy params, wholesale opt-in, usage counters), verify invalid configs are rejected with localized errors.

## 6. Order surfaces and regression

- [ ] 6.1 Show discount breakdown in admin/customer order details, confirmation emails, and PDF/HTML summary, verify a discounted order renders motives + amounts in all four surfaces.
- [ ] 6.2 Run full suite (`dotnet test VaultShop.sln`) and existing pricing/cart/checkout tests green with no-discount fallback unchanged, verify 0 regressions.
