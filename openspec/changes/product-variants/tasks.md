## 1. Data model and migration

- [x] 1.1 Add `VariantOptionType`, `VariantOptionValue`, `ProductVariant`, `ProductVariantValue` entities with navigations and verify `dotnet build VaultShop.sln` succeeds
- [x] 1.2 Add nullable `ShoppingCart.VariantId` + `OrderDetail.VariantId` + `OrderDetail.VariantLabel` and verify `dotnet build VaultShop.sln` succeeds
- [x] 1.3 Create EF Core migration for the new tables/columns and verify it applies to a scratch database with `dotnet ef database update`
- [x] 1.4 Seed-backfill check: existing products/carts/orders read with null variants and verify variant-less add-to-cart + checkout still pass on existing tests (`dotnet test VaultShop.sln`)

## 2. Admin variant management

- [x] 2.1 Admin UI to define per-product option types (pick existing or create) + values with sort order, verified by creating Casa/Tamaño values on a test product
- [x] 2.2 Cartesian "Generate combinations" producing explicit `ProductVariant` rows, verified by generating 8 rows for 4 Casa x 2 Tamaño values
- [x] 2.3 Per-combination available toggle + delete, and guarded value delete (blocked while referenced), verified by disabling one combination and attempting to delete a referenced value
- [x] 2.4 Server-side validation (variant belongs to product, covers exactly its types, value-per-type uniqueness), verified by unit tests posting mismatched/foreign variant ids

## 3. Storefront selection (mandatory)

- [x] 3.1 Details page renders one selector per option type with combination availability data, verified in browser for a 2-type product
- [x] 3.2 Add-to-cart requires explicit selection of every type (no default, no base-product purchase), verified by posting without selection and receiving the localized error with empty cart
- [x] 3.3 Variant-less product detail page renders no selectors and behaves as before, verified by existing storefront tests
- [x] 3.4 No variant URLs: links, slugs, sitemap, and SEO output unchanged, verified by asserting no new routes in sitemap tests

## 4. Cart keyed by variant

- [x] 4.1 Cart identity `(user, product, variant)`: different variants separate lines, same variant merges, verified by xUnit tests for split and merge cases
- [x] 4.2 `HomeController.Details` POST sums existing quantities across sibling variant lines against shared stock, verified by the 2+2-against-5 rejection test
- [x] 4.3 `CartController.Plus` accounts for sibling variant lines (product total + 1 vs stock), verified by the x2/x3-against-5 rejection test; Minus/Remove unchanged

## 5. Checkout and order snapshot

- [x] 5.1 Checkout validation sums per product across variant lines inside the existing transaction, verified by multi-variant checkout test
- [x] 5.2 Single shared-pool decrement per product by summed total (existing constraint mapping intact), verified by the 2+1-against-5 → stock 2 test
- [x] 5.3 `OrderDetail` persists `VariantId` + frozen `VariantLabel`, verified by renaming a value afterwards and asserting the old order still shows the original label
- [x] 5.4 Variant label displayed in order details, confirmation, summary HTML/PDF, and emails, verified by rendering each with a variant order

## 6. Full verification

- [x] 6.1 Concurrent checkouts do not oversell: the conditional relative decrement (`DecrementStockIfSufficient`, `WHERE StockQuantity >= total`) makes the DB arbitrate the race — verified by deterministic guard tests that fail if the write reverts to absolute. No true parallel-concurrency test exists: SQLite in-memory serializes writers, so a real race test needs PostgreSQL (out of scope for the suite).
- [x] 6.2 Full suite green (`dotnet test VaultShop.sln`) with no regressions in pricing, payments, shipping, or SEO tests
