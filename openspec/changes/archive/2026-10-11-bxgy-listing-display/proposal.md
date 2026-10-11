## Why

A BxGy promotion with `SameSkuOnly = false` (e.g. "2da al 70%", "2x1") leaks into the pre-cart listings: Home, featured sections, Search, and Favorites show the combo discount (strikethrough + discounted price, sometimes `$0`) on the cheapest listing items as if the shopper had already combined them. The storefront is pricing the whole listing as one cart. Confirmed locally: with a "2da al 70%" store promo, the cheapest listed product renders `$5.280 → $1.584` and a 2x1 promo renders `$0` before the first item is ever added. The only legitimately granted single-unit prices are direct offers and collection percentage offers; BxGy requires a group of ≥2 units, so it must never lower a unit price outside the cart.

## What Changes

- `StorefrontPricingService.GetDisplayPrices` evaluates **each product in isolation** (one line, `Count = 1`) instead of batching the entire listing into a single evaluation request.
- With one unit per evaluation, BxGy can no longer form a group, so it never produces a strikethrough price on Home / featured / Search / Favorites.
- BxGy visibility on listings is **badge-only**, driven by the existing coverage check (`FindCoveringBxGy`) — matching the behavior already documented in `_DiscountPrice.cshtml` and the service comment.
- Direct offers and collection percentage offers still apply to the single unit and still render strikethrough + badge.
- Cart/checkout evaluation is **unchanged** — mixed-product BxGy pooling in the cart is intended behavior.
- No data or schema changes; no new dependencies.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `catalog`: "Storefront shows sale and promotion badges" — a product listing inside a BxGy scope SHALL never show a granted combo price before the cart; BxGy is communicated as a coverage badge only.
- `pricing`: "Unified storefront price visibility" — the displayed single-unit price is the unit price (base → best eligible non-quantity discount); it never includes a BxGy grant, which requires ≥2 units in the cart.

## Impact

- `VaultShop.Web/Services/Pricing/StorefrontPricingService.cs` — `GetDisplayPrices` changes from one batched evaluation to per-product evaluation.
- `VaultShop.Web/Services/Pricing/DiscountEvaluator.cs` — **no change**; cart/checkout pooling semantics are preserved.
- Callers of `GetDisplayPrices`: `HomeController.Index` (listing + featured), `HomeController.Search`, `FavoriteController.Index`, `HomeController.Details` — behavior of `Details` is unchanged (it already passes one product).
- Views: no change (`_DiscountPrice.cshtml` already renders BxGy as badge-with-no-strikethrough when `EffectivePrice == BasePrice`).
- Tests: `StorefrontPricingServiceTests` gains multi-product listing coverage; existing single-product badge tests stay green.