## 1. Slug bookmarkability (B)

- [x] 1.1 Add optional `slug` param to `HomeController.Search`, resolve canonical via `Keyword.Slug`, redirect 301 on mismatch preserving `categoryId/searchString/pageNumber`, and expose `slug` to views via `ViewData`. Verify: `Search?keywordId=7&slug=wrong` redirects to `?keywordId=7&slug=naruto`; `Search?keywordId=7` returns 200.
- [x] 1.2 Update `_CollectionChips.cshtml` to include `asp-route-slug` (preserve) and `Search.cshtml` active context to display `/{slug}` alongside name + set `ViewData["Title"]` to include slug. Verify: chip href contains `slug=naruto` when active.
- [x] 1.3 Extend `_Pager.cshtml` to carry `slug` (`asp-route-slug`) and verify pager link on `?keywordId=7&slug=naruto&pageNumber=1` keeps `slug` on page 2.

## 2. Search filter rows additive (already partially on main)

- [x] 2.1 Confirm `HomeController.Search` `ViewData["Categories"]` and `Search.cshtml` double-row rendering (collections above categories) stays, and every chip/button preserves the other three filters (`keywordId/categoryId/searchString/slug`). Verify: from `?categoryId=2` clicking collection 5 yields `?keywordId=5&categoryId=2`; removing collection keeps `categoryId`.
- [x] 2.2 Add integration test `Search_AdditiveFilters_Preserve` asserting AND navigation URLs via rendered markup (or controller unit covering query preservation). Verify: `dotnet test` green.

## 3. Collection hero (Search only, reusable)

- [x] 3.1 Expose `CoverImageUrl` via `CollectionChipVM`/`HomeIndexVM.ComputeCollections` (already loads `Keyword.Images`) and add `ViewData["ActiveCollectionCover"]` in `Search` when `keywordId` active. Verify: `Search?keywordId=7` view model contains cover URL when exists.
- [x] 3.2 Create reusable partial `Views/Shared/_CollectionHero.cshtml` (breakout `width:100vw; margin-left:calc(50% - 50vw)`, `object-fit:cover center`, gradient scrim, responsive heights `400→80` / `260→52`, `html{overflow-x:clip}`). Render only when cover exists in `Search.cshtml` immediately below header outside `container`. Verify: no hero when cover missing; hero shows when cover present.
- [x] 3.3 Add vanilla `wwwroot/js/collectionHero.js` scroll handler (`passive` + `rAF`, `progress`, `--hero-h-current/--img-ty`, `prefers-reduced-motion` guard) and wire in layout/search. Verify: manual scroll collapses to thin strip; reduced-motion has no animation.

## 4. Row modernization & detail chips

- [x] 4.1 Add generic `.scroll-row` styles (`scrollbar-width:none`, `::-webkit-scrollbar{display:none}`, `scroll-behavior:smooth`, `mask-image` fade toggled by `data-at-start/end`) and vanilla `collectionChips.js` nav buttons (`scrollBy 0.8*clientWidth`, `ResizeObserver`). Verify: native bar hidden, buttons appear only when overflow, touch drag still works.
- [x] 4.2 Extend `HomeController.Details` include to `Keywords.Keyword.Images` and render collection chips in `Details.cshtml` below category badge linking to `Search?keywordId` (+ `slug` when available). Verify: product with 2 keywords shows 2 chips; product with 0 shows none.
- [x] 4.3 Final verification: `dotnet build` + `dotnet test VaultShop.sln` green, manual `Search` (both rows, additive clicks, pager keeps `slug`, hero collapse), `Details` chips, dark/light chip contrast, and home `Index` unaffected.

