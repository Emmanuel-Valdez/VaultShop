## 1. Offer persistence

- [x] 1.1 Copy `SaleRetailPrice`, `SaleWholesalePrice`, `SaleFromUtc`, and `SaleToUtc` in `ProductRepository.Update` and verify a repository or controller test reloads the four values after update.
- [x] 1.2 Keep the existing offer rules (1 to 1000000, below the same-channel final price, end not before start, empty dates always active) and verify an offer at or above the regular price still fails validation.

## 2. Culture decimals and localized selects

- [x] 2.1 Change offer, coupon value, coupon minimum subtotal, and promotion percent inputs to text with `inputmode="decimal"` and verify `es-AR` accepts `8000,50`, rejects `8000.50`, and `en-US` accepts `8000.50`.
- [x] 2.2 Replace `GetEnumSelectList` on coupon type, promotion kind, and promotion scope with localizer options and verify the Spanish and English labels in `specs/pricing/discounts/spec.md` render on the upsert forms. Stored enum values stay unchanged.

## 3. Variant picker and favorites

- [x] 3.1 Show the unavailable-combination message only when every selector has a value and the combination is missing or unavailable, and verify opening the detail page and returning after a successful add do not show it.
- [x] 3.2 Disable option values that cannot form an available combination with the current selection, using the existing combination JSON, and verify an impossible value is disabled without a server round-trip.
- [x] 3.3 Let the favorite button submit without variant selection (`formnovalidate` or its own form) and verify a signed-in shopper can favorite a variant product with empty selectors.
- [x] 3.4 Omit products with variants but zero available combinations from home and search, and hide add-to-cart on their detail page. Verify a variant-less product still lists, and favoriting the unsellable product still works.

## 4. Cart cleanup and description

- [x] 4.1 Remove cart lines whose variant fails `ValidateVariantForProduct` in both `RemoveShoppingCartsOutdated` methods, and verify opening the cart drops a newly unavailable variant while checkout still refuses one that races in at creation.
- [x] 4.2 Remove the product description from cart and favorites, and verify product detail still renders it.

## 5. Verification

- [x] 5.1 Run `dotnet test VaultShop.sln` and `openspec validate --change oferta-decimales-variantes` and verify both are clean.
