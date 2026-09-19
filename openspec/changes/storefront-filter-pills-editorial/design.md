## Context

Current storefront has two filter presentations: `.category-pill` (outline pill 40/31px, 28px thumb, `flex-wrap`) in `Home/Index.cshtml` + `Search.cshtml` via `Areas/Customer/Views/Home/_CategoryPill.cshtml`, and `.collection-chip` (72px circle, 6.5rem card, count badge, `collections-row scroll-row`) via `Views/Shared/_CollectionChips.cshtml` + `collectionChips.js`. `Views/Shared/_CollectionHero.cshtml` (duplicated under `Areas/Customer/Views/Shared/`) uses `100vw` breakout with `--hero-h-current` height animation in `collectionHero.js` causing layout thrash. `site.css` is the single stylesheet; `catalog/spec.md` governs Search filter rows, hero, and heading hierarchy. See `proposal.md` Why for motivation; artifact `.local/screenshot-preview/VaultShop_3_direcciones.html` direction B is the visual reference (chip vertical 72px, hero 320px, headings minimized, no count in chip).

## Goals / Non-Goals

**Goals:**
- Unify category + collection filters to one editorial chip component (72px circle, no count, 2-line clamp, 44px hit-targets, `scroll-row` horizontal on mobile).
- Relocate count from chip to `CollectionHero` (`Title /slug · N`) and results header (`Resultados (N)`).
- Minimize Search headings (single `h1`, hero demoted, filter rows `sr-only` or single `Filtrar por` muted) while keeping Home headings in editorial uppercase style.
- Fix hero animation to compositor-only (`transform`/`opacity`, no `height`) and consolidate `100vw` breakout fragility.

**Non-Goals:**
- No data model, migration, or API change. No new dependency. No change to filtering logic (`keywordId AND categoryId` stays). No admin `Category/Upsert` redesign (out of scope).

## Decisions

**D1 — Re-theme `_CategoryPill` to chip, not duplicate component.**
Reuse `_CategoryPill.cshtml` markup but switch its classes to `collection-chip` family (link, image, fallback, name) so Home and Search share one visual. Alternative considered: create new `_FilterChip` partial for both — rejected (more churn, same outcome; alias via CSS keeps diff smaller and preserves `CategoryPillVM`).

**D2 — CSS alias/extension vs full rewrite.**
Add `.category-pill` → `.collection-chip` alias or extend chip styles to cover category case in `site.css`, remove `.collection-chip__count` rendering, and unify `.collections-row` + filter rows under `.scroll-row` + `.scroll-row-wrap`. Alternative: keep two style families — rejected (duplication, divergent responsive behavior).

**D3 — Count removal is view-only.**
Keep `CollectionChipVM.Count` in the VM but stop rendering `__count` in chip; hero and header consume the count already passed via `ViewData["ActiveCollectionCount"]` and `PagedList.TotalCount`. Chip `title` may optionally carry `N productos` for hover. Alternative: remove count from VM — rejected (hero still needs it, unnecessary model churn).

**D4 — Headings: demote hero `h1` to `p` with display class.**
Change `_CollectionHero.cshtml` title from `h1` to `p.collection-hero__title[role="heading" aria-level="1"]` or styled `div`, so Search has single `h1` (Results). Search filter headings become `h2.sr-only` or single `p.filter-row__label`. Alternative: keep `h1` in hero and remove `h1` from Search header — rejected (Search header is the page title; hero is contextual).

**D5 — Hero height fixed, parallax via `transform` only.**
Set `--hero-h: 320px` (B editorial, responsive 320/260) fixed, remove `--hero-h-current` mutation in `collectionHero.js`; parallax becomes `translateY` on image + `opacity` on content with `will-change:transform`. Add `.u-breakout` utility (`width:100vw; margin-inline:calc(50% - 50vw)`) and scope `overflow-x:clip` comment, instead of global `html{overflow-x:clip}` reliance. Alternative: keep height animation — rejected (layout thrash, CLS risk).

**D6 — Scroll behavior: unify under `scroll-row`.**
Both filter rows get `collections-row scroll-row` + `scroll-row-wrap` so `collectionChips.js` mask/`data-at-start/end` + `ResizeObserver` applies uniformly. Buttons remain optional but `×` and chip links get `min-height:44px` and `focus-visible: 2px solid accent`.

## Risks / Trade-offs

- **Visual regression on Home** where pill was compact and now chip is taller (6.5rem) → Mitigation: keep Home chip row as `scroll-row` horizontal on mobile (no 2-line wrap), verify `site.css` `gap 1rem` and `scroll-padding-left` at `576-992px`; dust off `Home/Index.cshtml` section order (`Categories → Collections → All` unified).
- **Removing count from chip may surprise users who used it as signal** → Mitigation: count stays visible one scroll away in hero/header; `title` fallback retained; analytics after ship can re-evaluate.
- **Hero duplicate partial** may diverge again → Mitigation: make `Areas/Customer/Views/Shared/_CollectionHero.cshtml` a thin wrapper/alias or delete and keep single `Views/Shared/_CollectionHero.cshtml`; add comment.
- **100vw breakout + `overflow-x:clip` interaction** → Mitigation: `.u-breakout` utility with inline comment `// requires overflow-x:clip on root` and visual check `document.documentElement.scrollWidth === clientWidth` at 1280/390.
- **Touch target 44px increases row height** → Mitigation: keep visual circle 72px but expand hit-area via padding, not by enlarging card beyond 6.5rem.

## Migration Plan

- No migration. Change is view/CSS/JS only.
- Deploy: build, `dotnet test`, browser-check `localhost:8080/` and `/Home/Search` with `?keywordId` + `?categoryId` in both cultures and `≤575px`, light/dark, `prefers-reduced-motion`.
- Rollback: revert views + `site.css` + JS; no data to migrate.

## Open Questions

- None — B direction + count recommendation already decided; remaining details (exact uppercase tracking 0.12em, hero 320 vs 300) are CSS tuning during tasks.
