## Why

Saving a product offer does not persist, so the storefront never shows the sale. New admin decimal fields reject the culture's separator, promotion and coupon enums render as raw identifiers, and the variant picker treats an incomplete selection as unavailable. Those defects block selling and confuse shoppers now.

## What Changes

- Persist `SaleRetailPrice`, `SaleWholesalePrice`, `SaleFromUtc`, and `SaleToUtc` on product update. Existing offer rules stay: 1 to 1000000, strictly below the same-channel final price, end not before start, empty dates mean always active.
- Admin decimal inputs for the offer, coupon value, coupon minimum subtotal, and promotion percents accept one separator per culture: comma in `es-AR`, dot in `en-US`. Integer fields stay numeric.
- Coupon type, promotion kind, and promotion scope selects show localized labels in Spanish and English. Stored enum values do not change.
- The product-detail unavailable message appears only after every option type is selected and that combination is missing or unavailable.
- Favoriting a product does not require a variant selection and remains allowed when no combination is available.
- Option values that cannot form an available combination with the current selection are disabled in the picker. The combination JSON already on the page is the only source.
- A product with variants but zero available combinations is omitted from storefront listing and cannot be purchased. Favorite still works.
- Opening the cart and building checkout summary remove lines whose variant is no longer available, same as unavailable products. Checkout still refuses an unavailable variant if one races through.
- Cart and favorites do not render the product description. Product detail still does.
- Out of scope: a timed abandoned-cart sweeper, and a per-customer coupon cap. `MaxUses` stays a global order count.

## Capabilities

### New Capabilities

- `pricing/discounts`: offer fields actually persist; admin decimal entry follows the request culture; coupon and promotion selects use localized labels. The living spec does not have this capability until `descuentos-promociones` is synced. These requirements are ADDED so they append after that sync.

### Modified Capabilities

- `catalog`: variant picker feedback, favorite without a variant, hide unsellable variant products, omit description on cart and favorites.
- `stock-inventory`: outdated-cart cleanup also drops unavailable variants.

## Impact

- `VaultShop.DataAccess/Repository/ProductRepository.cs` must copy the four offer fields in `Update`.
- Admin views: `Product/Upsert.cshtml`, `Coupon/Upsert.cshtml`, `Promotion/Upsert.cshtml`, plus en/es resx. Selects are built from the localizer, not `GetEnumSelectList` display names.
- `Views/Shared/_ValidationScriptsPartial.cshtml` keeps rejecting the other culture's separator.
- `Areas/Customer/Views/Home/Details.cshtml` picker script; `FavoriteController` / favorite button must not share the cart form's required variant fields.
- Storefront queries in `HomeController` (index/search) exclude products whose every variant is unavailable.
- `CartController.RemoveShoppingCartsOutdated` and `CheckoutService.RemoveShoppingCartsOutdated`.
- `Favorite/Index.cshtml` and `Cart/Index.cshtml` drop the description.
- Sync `descuentos-promociones`, then `descuentos-correctitud-pagos`, before syncing or archiving this change. Implementation does not wait on that sync.
