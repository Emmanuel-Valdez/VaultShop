## 1. Data model and constants

- [ ] 1.1 Add `Product.FreeShipping` (bool, default `false`) and `OrderHeader.ShippingCharge` (decimal(18,2), default 0) with an EF migration that backfills existing products to `true`; verify on a fresh and an existing database that pre-migration products end up flagged, stored prices are untouched, and new inserts default to unflagged
- [ ] 1.2 Add `SD` constants for the Gran Mendoza canonical 26-locality list and the motorcycle (`M`) / vendor-arranged pickup (`V`) delivery types, plus a case- and accent-insensitive matcher; verify unit tests cover `LAS CANAS` ≡ `Las Cañas` and that a non-listed locality never matches
- [ ] 1.3 Add the free-shipping checkbox to the admin product upsert (unchecked by default on create) and verify the upsert tests persist both values

## 2. Pricing calculator

- [ ] 2.1 Gate the category shipping term in `PricingCalculatorService.GetFinalPrices()` on the product flag (margin and percentage-cost composition unchanged) and verify pricing tests: flagged suggestions include the shipping share (5000 → +5555.56 at 10% costs), unflagged exclude it, wholesale suggestions are identical for both
- [ ] 2.2 Verify `GetOutdatedProductCount`/`PublishSuggestedPrices` write per-flag suggestions and that no existing product becomes outdated at deploy because of the backfill
- [ ] 2.3 Update the admin price calculator help text to document the flag's effect; verify the help section renders the new sentence in Spanish

## 3. Shipping charge computation

- [ ] 3.1 Implement the charge + free qualification + struck-through standard amount in `CheckoutService` (include `Product.Category` in cart queries) as the single source for cart, summary, totals endpoint and order creation; verify tests: any free line → 0, paid-only cart → largest average once (not per unit/line), gross-up 5000 at 10% → 5555.56, discounts/coupons never alter the charge
- [ ] 3.2 Stamp `ShippingCharge` into `OrderTotal` in `CreateOrder` and freeze it; verify integration tests reconcile product total + charge and that the `OrderTotal <= 0` guard still holds
- [ ] 3.3 Append a non-discounted shipping line item in `BuildPaymentSessionLineItems` (quantity 1, excluded from payment-discount proration) and verify the session line sum equals the frozen `OrderTotal` for cases with and without payment discount

## 4. Checkout flow (Summary)

- [ ] 4.1 Replace the free-text province/locality domicile inputs with catalog selects (best-effort prefill from the stored profile) and collapse the branch picker into one province → locality selection; verify HTTP tests render locality options scoped to the province and reject a submitted pair absent from the catalog
- [ ] 4.2 Render modality options by locality and cart qualification: Gran Mendoza paid cart shows motorcycle + vendor pickup first then Correo; Gran Mendoza free cart shows motorcycle and Correo at $0 and no vendor pickup; other localities show Correo only; verify rendering tests for each case
- [ ] 4.3 Extend `SummaryTotals` to update the summary when the modality changes (vendor pickup removes the charge; Correo/motorcycle keep it; free cart stays $0); verify an HTTP/browser test observes the total change and the charge line disappearing
- [ ] 4.4 Persist the modality (`S`/`M`/`V`) and snapshots at creation: branch required and snapshotted only for Correo; motorcycle/vendor orders store no branch; vendor pickup records the Microcentro wording; verify order persistence tests including a vendor-pickup order (charge 0) and a Maipú motorcycle order
- [ ] 4.5 Verify Company checkout is unchanged: branch still required, no charge added, no modality options; verify an existing company-checkout test still passes unchanged

## 5. Cart and storefront display

- [ ] 5.1 Add the cart shipping line: free carts show the free label with the standard amount struck through and no total impact; paid carts add the charge like an item; no explanatory notes; verify cart rendering tests in es and en
- [ ] 5.2 Add the free-shipping badge to `_DiscountPrice` for flagged products and verify it appears on Home, Search, Favorites and Details for retail viewers and never for Company users or wholesale preview

## 6. Order surfaces

- [ ] 6.1 Show the frozen shipping charge and modality on order confirmation, customer history and admin order detail, keeping the branch snapshot block for `S` orders and a modality line for `M`/`V`; verify rendering tests including a pre-change order (no charge, no modality) that must not break
- [ ] 6.2 Render the shipping line and modality in `OrderSummaryService` + PDF from persisted values only; verify the summary/PDF tests assert the amount, the modality and the unchanged grand total
- [ ] 6.3 Add the shipping line to the confirmation and bank-transfer instruction emails when the charge is greater than zero; verify email content tests show it and that free orders render without a shipping amount

## 7. Copy and help

- [ ] 7.1 Update FAQ answers (shipping methods, delivery times, pickup, and the free/paid rule) in es and en; verify the FAQs page renders the new copy in both cultures
- [ ] 7.2 Finalize the new resx labels (modality names, cart free/paid lines, receipt wording, validation errors) in es and en; verify no localized key falls back to the English default at runtime

## 8. Regression and end-to-end verification

- [ ] 8.1 Run the full test suite and verify no existing pricing, checkout, payment-session, order-view or email test regresses
- [ ] 8.2 Manually walk the agreed scenarios on a local instance: free-shipping cart (badge, free line, $0), paid-only cart (charge in cart, checkout and total), Gran Mendoza modalities (motorcycle, vendor pickup removing the charge), outside-Gran-Mendoza locality (Correo only), and a wholesale order (no charge, branch required)
