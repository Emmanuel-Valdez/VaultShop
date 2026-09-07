# Tasks: stock-visibility-storefront

## 1. Card Out-of-Stock Banner

- [x] 1.1 Add `.product-card__stock-badge` block to `wwwroot/css/site.css` (absolute bottom strip, translucent background, localized-safe uppercase text) with a `[data-bs-theme=dark]` override — verify the badge overlays the card image without shifting layout at any viewport width
- [x] 1.2 Render the banner inside `.product-card__image-link` in both card loops of `Areas/Customer/Views/Home/Index.cshtml` when `product.StockQuantity == 0` (icon `aria-hidden="true"` + `@Localizer["OutOfStock"]`) — verify a 0-stock product shows the strip and an in-stock product shows nothing
- [x] 1.3 Render the banner in the card loop of `Areas/Customer/Views/Home/Search.cshtml` under the same condition — verify via search results page
- [x] 1.4 Add `OutOfStock` key to `Index.en.resx`/`Index.es.resx` and `Search.en.resx`/`Search.es.resx` ("Out of stock" / "Sin stock") — verify both cultures render the banner text correctly
- [x] 1.5 Add `aria-label="@product.Name"` to the Search card image link (pre-existing gap, opportunistic fix per design D1 note) — verify via accessibility inspection that SR announces name + stock state

## 2. Detail Page Quantity & Structured Data

- [x] 2.1 Add `StockAvailable` key (`"{0} available"` / `"{0} disponibles"`) to `Details.en.resx`/`Details.es.resx` — verify placeholder renders as a plain integer in both cultures
- [x] 2.2 Render the available-quantity line after the price block in `Areas/Customer/Views/Home/Details.cshtml` only when `Model.Product.StockQuantity > 0` — verify an in-stock product shows the number and a 0-stock product shows neither the line nor any duplicate of the existing badge
- [x] 2.3 Fix JSON-LD availability in `Details.cshtml` to `"https://schema.org/OutOfStock"` when `StockQuantity == 0`, else `"https://schema.org/InStock"` — verify the rendered `<script type="application/ld+json">` reflects each state

## 3. Verification

- [x] 3.1 Manually verify light theme, dark theme, and mobile viewport on Home, Search, and Details for both stock states — verify no layout shift and banner remains readable
- [x] 3.2 Run `dotnet build --no-restore` and `dotnet test VaultShop.sln` — verify full suite stays green
