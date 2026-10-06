## Why

Products that come in multiple options (size, color, house, capacity, material, model, …) can only be sold today by duplicating the entire product, which fragments stock, pricing, and catalog browsing. A generic, per-product variant system removes the duplication while keeping one price, one stock pool, and one photo gallery per product.

## What Changes

- New generic variant model: reusable option-type names (`VariantOptionType`), per-product values (`VariantOptionValue`), explicit purchasable combinations (`ProductVariant` + `ProductVariantValue`).
  - Admins create type names freely (no hardcoded Size/Color); each product defines its own values under the types it uses.
  - Typical size is 2–10 combinations per product; admin UI prioritizes simplicity over bulk editing.
- Storefront product Details page renders one selector per option type used by the product; when a product has variants, the customer MUST explicitly select one value per type before adding to cart (no default / base-product purchase).
- Cart identity becomes `(ApplicationUserId, ProductId, VariantId)`; the same product with different variants lives on separate cart lines.
- Stock stays a single shared pool on `Product`: all existing per-product stock guards sum quantities across that product's variant rows instead of reading a single row.
- Same price and same photos for all variants in v1 (no per-variant price, stock, or images).
- `OrderDetail` records `VariantId` plus a frozen `VariantLabel` text snapshot (e.g. `Casa: Gryffindor, Tamaño: 15"`), immutable like `Price` — renaming or disabling a variant never rewrites history. Disabling a variant blocks new adds only.
- `ProductVariant` carries only `IsAvailable` in v1; it is the deliberate extension point for future per-variant price/stock columns without redesign.

## Capabilities

### New Capabilities

- `catalog/product-variants`: generic variant model (types, per-product values, combinations), admin CRUD for a product's variants, storefront selectors with mandatory selection, cart lines keyed by variant.

### Modified Capabilities

- `stock-inventory`: per-product stock guards (add-to-cart, cart quantity change, checkout validation/decrement) sum quantities across all of the product's variant rows; decrement still applies once per product.
- `order-lifecycle`: `OrderDetail` gains immutable variant snapshot (`VariantId` + `VariantLabel`); frozen-after-shipped semantics extend to the variant label.
- `catalog`: product Details page gains variant selectors and requires explicit selection when variants exist.

## Impact

- Data: 4 new tables + `ShoppingCart.VariantId` (nullable, null = legacy/variant-less row) + `OrderDetail.VariantId` (nullable) + `OrderDetail.VariantLabel` (snapshot string); EF Core migration with backfill (existing rows keep null variant).
- Code: `Customer/HomeController.Details` (POST), `Customer/CartController` (add/plus/minus/remove, session count), `CheckoutService` (validation + decrement + `OrderDetail` creation), admin product CRUD views, Details view + JS variant resolution, order/emails/PDF variant label display.
- No changes to payment providers, webhooks, pricing engine, shipping/branch pickup, or SEO/slug behavior; variants never get their own URLs.
