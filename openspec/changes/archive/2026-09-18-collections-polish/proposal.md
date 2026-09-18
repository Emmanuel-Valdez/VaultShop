## Why

The `keywords-collections` feature shipped functional but with polish gaps that hurt discoverability and UX: `Slug` is stored but never shown and its admin hint contradicts the `Required` validation (English fallback), the cover image is never rendered, the `keywordId+categoryId` AND filter works server-side but the storefront links drop the other filter, collection chips have clipped hover, light-on-light dark-mode contrast and uneven widths from long names, and product `Details` never surfaces its collections. A small fix batch already landed (slug `ValidateNever`, `AND` preservation, chip `padding/wrap/dark`); this change formalizes the remaining visual and navigation polish that makes the transversal taxonomy feel intentional.

## What Changes

- **Slug becomes visible and bookmarkable**: `Search` accepts optional `slug` query param alongside `keywordId` (filter stays by Id), link helpers preserve it, pager keeps it, mismatch redirects to canonical `?keywordId=&slug=canonical`, and the active collection name + `/{slug}` appears in the search tab/breadcrumb.
- **Cover hero on collection search**: when `keywordId` is active and a `Cover` image exists, render a reusable full-width breakout hero immediately below the navbar (`object-fit:cover center`, consistent heights `~400→80` desktop / `260→52` mobile) with progressive collapse on scroll (height + `translateY` parallax, text fade, `prefers-reduced-motion` fallback). Without cover → no hero (no empty box).
- **Storefront filter rows become additive and always visible**: `Search` always shows both rows — collections (chips) above categories (pills/images) — each chip/link preserves the other filters (`keywordId`/`categoryId`/`searchString`/`slug`) via `AND`; active states highlighted with `aria-current`.
- **Product detail surfaces collections**: `Details` includes `Keywords.Keyword.Images` and renders linkable collection chips below the category badge (nothing when none).
- **Collection row modernized (lightweight, no new deps)**: hide native scrollbar (`scrollbar-width:none` + `::-webkit-scrollbar`), `mask`/`fade` edges, smooth `scrollBy` nav buttons (vanilla, `ResizeObserver` + `passive` scroll), `scroll-snap` retained; homogeneous chip width `6.5rem` + ` -webkit-line-clamp:2` for long names.
- **Admin hint fix is out of scope for this spec** (already shipped as minimal fix: `ValidateNever` + `ModelState.Remove`) — noted here for traceability.

## Capabilities

### New Capabilities
- `catalog/collection-hero`: reusable hero component for collection cover images — when to show, layout/breakout, responsive heights, scroll collapse/parallax and motion-reduction. No new backend storage; reuses `KeywordImage Kind.Cover` (`keywords/keyword-{id}`).

### Modified Capabilities
- `catalog`: storefront filter UX — slug-aware URLs and display, additive `keywordId+categoryId` navigation with both rows always visible, pagination preserves all filters including `slug`, and product detail shows collection links.

## Impact

- **Models**: `HomeIndexVM`/`CollectionChipVM` expose `CoverImageUrl` + `Slug` (already present, now rendered); `HomeController.Index/Search/Details` include `Keywords.Keyword.Images`.
- **Views**: `Search.cshtml` (hero, double rows, slug in heading), `_CollectionChips.cshtml` (preserve `slug`/other filters, homogeneous width), `_CategoryChips` reuse via generic `.scroll-row`, `Details.cshtml` (collection chips), `_Pager.cshtml` (preserve `slug`).
- **CSS/JS**: `site.css` (row `padding-top`, dark `surface-dark` chip bg `#1e2030`, `line-clamp`, scrollbar hide + fade, hero breakout `100vw`), vanilla hero + carousel JS (~40 LOC, `requestAnimationFrame`).
- **Storage**: reuse `IImageStorageService` prefix `keywords/keyword-{id}`; `KeywordImageService` future cover target `1920×800` cover-crop (not in v1 of this change — noted as follow-up).
- **Dependencies**: none added. No migration (uses existing `Keyword.Slug` partial unique index, `KeywordImage` unique `(KeywordId,Kind)`).
