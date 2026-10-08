## 1. Existing tabs

- [ ] 1.1 Rewrite the Spanish category section so it covers slug, image, and average shipping cost, and does not assign monthly expectation to the category. Verify the rendered help text.
- [ ] 1.2 Rewrite the Spanish product-creation section so it covers per-product monthly expectation, stock, store availability, the offer window, collections, and variants. Verify the rendered help text.
- [ ] 1.3 Correct `_HelpCosts` and `_HelpFinalPrices` only where they still place expectation or shipping on the wrong entity. Verify those partials no longer contradict the product and category sections.

## 2. New tabs

- [ ] 2.1 Add Spanish partials and tabs for offers, promotions, coupons, variants, and collections. Coupon max uses must be described as a global order cap, not per customer. Verify each tab is reachable from help.
- [ ] 2.2 Leave the help body in Spanish under `en-US`. Verify switching culture does not require English help copy.

## 3. Verification

- [ ] 3.1 Open help after `oferta-decimales-variantes` and verify the offer and promotion tabs describe the fixed forms. Run `openspec validate --change ayuda-admin-es`.
