## 1. Code — Isolate storefront display evaluation

- [x] 1.1 Rewrite `StorefrontPricingService.GetDisplayPrices` to evaluate each product in isolation (one line, `Count = 1`) while keeping the single shared active-promotions query, and remove the now-unneeded `byProduct` grouping. Verify `dotnet build VaultShop.sln` succeeds and the badge fallback to `FindCoveringBxGy` is preserved for products whose lines carry no discount.

## 2. Tests — Multi-product listing display

- [x] 2.1 Add a multi-product helper (`GetPrices(products, useWholesale)`) to `StorefrontPricingServiceTests` and a test asserting a mixed `SameSkuOnly=false` BxGy listing shows **no strikethrough** on any product and **the badge on every covered product`. Verify the test fails against the pre-change batching behavior and passes after 1.1.
- [x] 2.2 Add a test asserting that in the same mixed listing a product with an active **direct offer** still shows offer price + strikethrough + badge, and a product under a **collection percentage offer** still shows its per-unit discount. Verify both pass after 1.1.
- [x] 2.3 Run `dotnet test VaultShop.sln` and confirm the full suite is green (existing single-product badge tests in `StorefrontPricingServiceTests` must stay green without edits unless they depend on the old grouping).

## 3. Spec sync

- [x] 3.1 Update `openspec/specs/catalog/spec.md` and `openspec/specs/pricing/spec.md` from the delta specs (archive-time step; verify wording matches the delta requirement headings exactly).

## 4. Verification

- [x] 4.1 Run `openspec validate --change 2026-10-11-bxgy-listing-display` and confirm the change passes before archiving.
- [x] 4.2 Manual smoke: with a `2da al 70%` (mixed BxGy) promo active, load Home, featured, Search, and Favorites in local and confirm no combo strikethrough / `$0` renders pre-cart, while `Details` and cart totals are unchanged.