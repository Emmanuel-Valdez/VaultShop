# Proposal: stock-visibility-storefront

## Why

Stock guards landed with `stock-inventory`, but shoppers only discover a product is out of stock after entering its detail page. Product cards on Home and Search render no stock signal, and the detail page hides the available quantity that explains why the quantity stepper stops — causing avoidable clicks, dead-end navigation, and mismatched SEO metadata (JSON-LD hardcodes `InStock` for every product).

## What Changes

- Out-of-stock products show a translucent banner strip over the card image on storefront product cards (Home and Search), so availability is visible without opening the product.
- The product detail page shows the available stock quantity next to the price when stock is greater than zero, closing the loop with the stock-capped stepper.
- JSON-LD `availability` on the detail page reflects actual stock (`InStock` / `OutOfStock`) instead of always advertising `InStock`.
- No low-stock banners, no exact-quantity display on cards, no cart-page stock indicators — out-of-stock is the only stock fact rendered on cards.

## Capabilities

### New Capabilities
- `ux/stock-visibility`: Stock availability signals on storefront product surfaces — out-of-stock banner on product cards (Home, Search), available-quantity display on the product detail page, and truthful JSON-LD availability metadata.

### Modified Capabilities
- _(none — `ux/product-detail-quantity` controls stepper behavior only; the quantity display is additive presentation, not a stepper requirement change)_

## Impact

- **Storefront views:** `VaultShop.Web/Areas/Customer/Views/Home/Index.cshtml` (2 card loops), `Areas/Customer/Views/Home/Search.cshtml` (1 card loop), `Areas/Customer/Views/Home/Details.cshtml` (price block + JSON-LD).
- **Styling:** `VaultShop.Web/wwwroot/css/site.css` — one `.product-card__stock-badge` block with dark-theme override. No new JS, no new dependencies.
- **Localization:** new resx keys `OutOfStock` (Index/Search views, en+es) and `StockAvailable` (Details view, en+es).
- **Data:** none — `Index`/`Search` actions already return full `Product` entities including `StockQuantity`; no controller or query changes.
- **Tests:** view-level assertions optional; `dotnet test` must stay green.
- **Out of scope:** Cart page stock indicators, low-stock thresholds/alerts, stock quantity on cards.
