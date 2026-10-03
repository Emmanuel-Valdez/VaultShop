## Why

Product pages still live at id-only URLs (`Details?productId=123`, sitemap `/Home/Details/{id}`) while collections already enjoy canonical slug URLs (`Search?keywordId=7&slug=naruto`). This change extends the proven keyword-slug pattern to products and categories so storefront URLs are SEO-friendly, shareable, and self-describing.

## What Changes

- Add nullable `Slug` to `Product` and `Category` (same semantics as `Keyword.Slug`: `Slugify(empty ? Name : Slug)`, uniqueness among non-deleted rows).
- Extract the shared slug-resolution block from `KeywordController` into a reusable helper (e.g. `SlugHelper.ResolveSlugOrDefault`) and reuse it in keyword/product/category upserts.
- Product detail URL becomes `/Details/123/mi-remera` (id + slug): id is truth, slug is decorative; slug mismatch 301-redirects to canonical, missing slug renders without redirect (links/pager add it next navigation).
- Category gains full slug detail: canonical category URL (id + slug, mirroring the collection pattern) with the same mismatch-301 behavior; `categoryId` remains the filter truth.
- All 19 storefront links (`Index`, `Search`, `Cart`, `Favorite`) render the canonical slug form; sitemap emits canonical product/category URLs; `<link rel="canonical">` agrees with sitemap.
- Backfill migration auto-generates slugs for existing products/categories with collision suffixing (`-2`, `-3`).
- Fix the 2 deferred CS8601 warnings (`HomeIndexVM.cs:24`, `HomeController.cs:110`) by coalescing nullable slugs.

## Capabilities

### New Capabilities

- `catalog/product-slugs`: product + category slug fields, id+slug canonical URLs, mismatch-301, backfill, sitemap/canonical updates.

### Modified Capabilities

- `catalog`: storefront `Details` links and `Search` category-filter links carry slugs; pagination preserves product/category slugs alongside existing `slug` behavior.

## Impact

- Affected code: `Product`/`Category` models + EF migration, `SlugHelper`, admin `Product`/`Category`/`Keyword` upserts, `HomeController.Details` + `Search`, 19 `.cshtml` product links, category chips, `SeoController` sitemap, pager partial.
- No payment/auth/order behavior changes. Old id-only URLs keep working (no redirect when slug absent) so bookmarks and external links survive.
