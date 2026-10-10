## 1. Session charges OrderTotal

- [ ] 1.1 Prorate `PaymentDiscountTotal` across the session line items in `CartController` (session request only; `OrderDetail` rows and header untouched) and verify a test with an active payment-method discount asserts the session lines sum exactly to `OrderTotal`.
- [ ] 1.2 Verify a test with no active payment-method discount asserts session construction behavior is unchanged.

## 2. Pre-confirmation visibility

- [ ] 2.1 Announce the discount on the picker's method card from the active promotions (e.g. "−10% paying by bank transfer") and verify it renders for a method with an active promotion and renders nothing for one without.
- [ ] 2.2 Add the Summary method-change AJAX re-evaluation (reusing `BuildSummary` with the selected method) updating the discount row and final total, and verify the row and total change before Place Order.
- [ ] 2.3 Render the note under the selected payment method stating that method's current offer, and verify it appears for the selected method only.

## 3. FAQ stacking documentation

- [ ] 3.1 Add Q11 to `FAQs.cshtml` plus `Question11`/`Answer11` in `FAQs.{es,en}.resx` (specifics do not stack — best price wins; payment-method discount stacks on top) and verify the accordion item renders in both cultures.

## 4. Verification

- [ ] 4.1 Run the flow test suite plus `openspec validate descuento-medio-de-pago` and verify both are clean.
