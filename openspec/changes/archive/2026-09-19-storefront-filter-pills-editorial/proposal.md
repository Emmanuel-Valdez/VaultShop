## Why

Storefront filtering uses two competing visual languages: category pills (28px thumb, 40/31px height, wrap) vs collection chips (72px circle, 104×147 card with count). UX audit (Playwright localhost:8080 + static review) found <44px touch targets, order flip Home vs Search, redundant counts in every chip, and headings that add no value in Search. Editorial direction B (chip vertical 72px unified) was chosen to consolidate filters into a single brand object, with count moved from chip to contextual hero/header.

## What Changes

- Unify category + collection filters to the **editorial chip** (72px circle `object-fit:cover`, 6.5rem card, 2-line name clamp) — `_CategoryPill` re-themed to use `.collection-chip` styles; Home and Search both render the same chip component.
- Remove `collection-chip__count` / `category count` from every chip; count appears only in `CollectionHero` (`Title /slug · N productos`) and in the results header (`Resultados (N)` / `All products (N)`). Chip `title` may keep `N productos` for hover fallback.
- Minimize headings in Search: remove `Shop by Category` / `Shop by Collection` `h2` noise; replace with a single `Filtrar por` 0.75rem uppercase `tracking 0.12em` muted or `sr-only` heading per row, so filter hierarchy is visual not textual. Home keeps headings but restyled to editorial uppercase variant.
- Fix P0 accessibility: chip `min-height 44px` equivalent hit-area, `×` remove 44px hit-area (visual 26px inside), `focus-visible` accent ring, filter rows become `scroll-row` horizontal with `overflow-x:auto + mask fade` and no wrap on `≤575px`.
- Keep hero breakout 100vw but fix layout thrash: animate only `transform`/`opacity` (no `height`), and resolve duplicate `html{overflow-x:clip}` fragility with a scoped `u-breakout` utility.

## Capabilities

### New Capabilities
- (none — behavior is a refinement of existing catalog presentation)

### Modified Capabilities
- `catalog`: storefront filter presentation — unify pill/chip to editorial chip, relocate count from chip to hero/header, minimize Search headings, fix touch/focus/scroll behavior for filter rows.

## Impact

- Views: `VaultShop.Web/Areas/Customer/Views/Home/Index.cshtml`, `Search.cshtml`, `Areas/Customer/Views/Home/_CategoryPill.cshtml` (re-theme to chip), `Views/Shared/_CollectionChips.cshtml`, `Views/Shared/_CollectionHero.cshtml`, `Areas/Customer/Views/Shared/_CollectionHero.cshtml` (duplicate), partials order Home vs Search.
- Styles: `VaultShop.Web/wwwroot/css/site.css` — `.category-pill` re-theme or alias to `.collection-chip`, `.collections-row`/`.scroll-row` consolidation, `collection-hero--breakout` height vars, `u-breakout`, `focus-visible`, responsive `≤575px`.
- JS: `wwwroot/js/collectionChips.js` + `collectionHero.js` — remove height animation, keep `transform` only.
- A11y/SEO: single `h1` in Search (hero heading demoted), `aria-current`, `aria-label` on `×`.
- No DB/migration, no API/breaking change, no new dependency.
