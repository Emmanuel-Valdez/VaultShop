## 1. Styles — unify pill→chip, scroll-row, headings, hero

- [x] 1.1 Extend `site.css` editorial chip as the single filter style: alias `.category-pill` to `.collection-chip` family (or re-theme pill to chip: 72px circle, 6.5rem card, `clamp 2 lines`, `gap 0.45rem`), remove `.collection-chip__count` rendering reliance, add `min-height:44px` hit-area for chip link and `×` (visual 1.6rem inside 44px hit-area), add `focus-visible: 2px solid var(--theme-accent)` for `.collection-chip__link`/`.category-pill` and verify `dotnet build VaultShop.sln` succeeds and no Sass/build error
- [x] 1.2 Unify filter rows under `scroll-row` + `scroll-row-wrap`: ensure both Home and Search category+collection rows use `collections-row scroll-row` with `overflow-x:auto`, `mask fade`, `scroll-snap proximity`, `scroll-padding-left` 0.5rem (0.75rem at 576-991px), `-webkit-overflow-scrolling:touch`, no wrap at `≤575px`, and verify in browser at 390px rows scroll horizontally with fade and no wrapping
- [x] 1.3 Add `.u-breakout` utility for hero and fix hero vars: set `--hero-h: 320px` (B editorial, 320/260 responsive), fixed `height: var(--hero-h)` (remove `--hero-h-current` height animation), add `overflow:hidden` + `will-change:transform` on image, and verify `html{overflow-x:clip}` comment and `document.documentElement.scrollWidth === clientWidth` at 1280 and 390

## 2. Hero + count relocation

- [x] 2.1 Update `Views/Shared/_CollectionHero.cshtml` (and duplicate `Areas/Customer/Views/Shared/_CollectionHero.cshtml` — consolidate to one or thin wrapper) to demote title from `h1` to `p.collection-hero__title[role="heading" aria-level="1"]` with `clamp(1.8rem,4vw,2.4rem)` Playfair, keep `Count` as `· N productos` plus `Slug`, keep `fetchpriority high eager`, and verify Search with `?keywordId` shows hero with `Title /slug · N` and page has exactly one `h1`
- [x] 2.2 Update `Views/Shared/_CollectionChips.cshtml` to stop rendering `.collection-chip__count` (keep `CollectionChipVM.Count` in model for hero/header, optionally add `title="N productos"` on link) and verify chips show only name (no `(N)`), `aria-current` + `×` with `aria-label` remain

## 3. Search headings + results header

- [x] 3.1 Update `Areas/Customer/Views/Home/Search.cshtml` filter sections: replace `h2 Shop by Category` / `Shop by Collection` visible headings with `h2.sr-only` or single `p.filter-row__label` `0.75rem uppercase tracking 0.12em muted` ("Filtrar por"), keep `aria-label` on rows, and verify no visible `h2 Shop by…` in Search while chips still navigate correct query
- [x] 3.2 Update Search results header to carry count: render `Resultados (N)` / `Search Results (N)` from `Model.TotalCount` (or `ViewData["ActiveCollectionCount"]` when filtered) alongside `termText`, and verify `?keywordId=7&slug=naruto` shows count in header and in hero, not in chips

## 4. Home + Search filter order + pill re-theme

- [x] 4.1 Update `Areas/Customer/Views/Home/Index.cshtml` and `Search.cshtml` to render categories and collections in unified order (Categories before Collections in both — B editorial: two separate `scroll-row` rows with editorial uppercase row labels `Colecciones` / `Categorías` `0.75rem uppercase tracking 0.12em` on Home, minimized on Search), and re-theme `Areas/Customer/Views/Home/_CategoryPill.cshtml` markup to `collection-chip` classes (image circle 72px, fallback `char.IsLetter`, `IsActive` → `active`, preserve `keywordId/slug/searchString` routing via `CategoryPillVM`) and verify Home `?categoryId` and Search `?keywordId&categoryId` both render unified chips and preserve additive `AND` navigation and `×` removal

## 5. Hero JS — compositor-only parallax

- [x] 5.1 Update `wwwroot/js/collectionHero.js` to remove `--hero-h-current` height mutation, animate only `--img-ty` (`translateY`) + `--content-opacity` (`opacity`) via `requestAnimationFrame`, keep `prefers-reduced-motion` guard, and verify no layout thrash (profiler shows `transform`/`opacity` only, no `height` recalc) and `prefers-reduced-motion` disables parallax

## 6. Verification

- [x] 6.1 Run `dotnet test VaultShop.sln` and verify full suite green (no filter logic change); browser-check `localhost:8080/` and `localhost:8080/Home/Search` with `?keywordId=&slug=` + `?categoryId` in `es-AR`/`en-US`, at 1280 + 390 + dark mode, verifying: single `h1` in Search, no chip count, counts in hero/header, 44px hit-targets measurable, `focus-visible` rings, `scroll-row` mask fade, no horizontal overflow, and `Details` collection chips consistent
