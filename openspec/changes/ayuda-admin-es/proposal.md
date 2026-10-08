## Why

The admin help page still describes category-level monthly expectation and omits offers, promotions, coupons, variants, and collections. An admin following it will misconfigure the store. Spanish copy is enough; English help is out of scope.

## What Changes

- Rewrite the existing Spanish help tabs so product creation and categories match the current admin: slug, category image, average shipping cost, per-product monthly expectation, stock, store availability, offer window, keywords/collections, and variants.
- Add Spanish help tabs for offers, promotions, coupons, variants, and collections.
- Do not translate the help page to English in this change.
- Do not change admin behavior. This is guidance only.

## Capabilities

### New Capabilities

- `ux/admin-help`: Spanish admin help matches the operations an admin can actually perform.

### Modified Capabilities

- None.

## Impact

- `VaultShop.Web/Areas/Customer/Views/Home/Help.cshtml` and the shared partials it includes (`_HelpProducts`, `_HelpCosts`, `_HelpFinalPrices`), plus new Spanish partials for the new tabs.
- No model, pricing, or checkout changes.
- Apply after `oferta-decimales-variantes` so the offer and promotion help describes the fixed forms, not the broken ones.
