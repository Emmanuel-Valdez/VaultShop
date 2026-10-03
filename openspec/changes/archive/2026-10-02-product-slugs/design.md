## Context

See proposal.md for motivation. Current state: `Keyword.Slug` (nullable, unique index among active rows) with inline upsert logic (`KeywordController.cs:56-69`), canonical 301 on `Search?keywordId=&slug=` mismatch, and id-only product URLs (`Details(int productId)`, 19 `asp-route-productId` links, sitemap `/Home/Details/{id}`). One default route (`{culture}/{area}/{controller}/{action}/{id?}`) with an `es-AR|en-US` culture constraint. `SlugHelper.Slugify` drops non-decomposable chars (CJK → empty), covered by `SlugHelperTests`.

## Goals / Non-Goals

**Goals:**
- Mirror the proven keyword-slug behavior for products and categories with id-anchor URLs.
- One shared slug-resolution helper reused by all three upserts.
- Zero broken bookmarks: id-only URLs keep rendering.

**Non-Goals:**
- Slug-only pretty routes (`/products/x`) — explicitly deferred; id stays in URL.
- Slug history table — renames 301 from the id anchor, old slugs are not retained.
- Category restructuring or filter-semantics changes beyond adding the slug param.

## Decisions

1. **URL shape: `/Details/{productId}/{slug}` as optional path segment, not query.**
   Rationale: matches the requested `/Details/123/mi-remera` form; keeps `productId` binding intact. Alternative (query `?slug=`) considered — same semantics but uglier and inconsistent with the ask. Path-segment requires an explicit route (or optional `{slug?}` on the Details action) ordered before the default route; query would need none. Chosen path-segment for the requested readability.
2. **Category detail via `Search?categoryId=&cslug=` canonical form, not a new page.**
   Rationale: categories today are a search filter, not a page; building a dedicated category page duplicates Search. A `cslug` decorative param mirrors the collection `slug` pattern exactly (mismatch → 301, absent → 200). Alternative (new `Category/Details` page) rejected as scope creep.
3. **Shared helper `SlugHelper.ResolveSlugOrDefault` (pure, tested).**
   Rationale: the keyword block (empty→Name, Slugify, blank-check) is copy-paste bait for two more upserts. Pure function returning `(slug, errorKey?)` keeps controllers thin and unit-testable. Uniqueness check stays in controllers (needs DB).
4. **Uniqueness scoped per entity among non-deleted rows (products | categories | keywords separately).**
   Rationale: consistent with keywords; lets a product and a category share a slug since their URL namespaces differ (`/Details/` vs `Search?categoryId=`). Global uniqueness rejected — unnecessary coupling.
5. **Backfill in-migration with `-2` suffixing; no admin review step.**
   Rationale: catalog is small; deterministic migration beats a manual pass. Products with CJK-only names (Slugify → empty) fall back to `producto-{id}` so the NOT NULL invariant holds.
6. **Fix the 2 deferred CS8601s (`HomeIndexVM.cs:24`, `HomeController.cs:110`) inside this change.**
   Rationale: adding two more nullable slugs without fixing the coalescing pattern triples the warning surface. `?? string.Empty` at the projection sites.

## Risks / Trade-offs

- [Risk] Custom slug route shadows default route or breaks culture prefix → Mitigation: constrain to `Details/{productId:int}/{slug?}`, register before default, cover both cultures in route tests.
- [Risk] 19 cshtml links missed → id-only URLs render (works but splits canonical coverage) → Mitigation: grep audit task + sitemap/canonical test asserting no bare `/Details/{id}"` links in rendered HTML.
- [Risk] Duplicate names at backfill produce surprising `-2` slugs → Mitigation: deterministic order by Id, logged; acceptable — admin can rename after.
- [Risk] `Slugify` empties CJK names → Mitigation: `producto-{id}` fallback, documented in spec.
- Trade-off: no slug-history means renamed products lose old-slug 301s (only id URLs survive). Accepted: id anchor covers bookmarks; history table is a later change if SEO demands it.

## Migration Plan

1. Deploy code + migration together: columns nullable-first? No — migration adds nullable `Slug`, backfills, then creates unique filtered indexes (`WHERE IsDeleted=false`); app code treats null as "needs generation on next upsert".
2. Rollback: drop columns/indexes; id-only URLs render throughout (forward-compatible since missing slug → 200).
3. Post-deploy: fetch `/sitemap.xml`, spot-check 301 on a wrong-slug URL in both cultures.
