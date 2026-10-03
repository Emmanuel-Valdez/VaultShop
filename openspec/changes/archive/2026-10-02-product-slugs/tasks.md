## 1. Shared slug helper + model/migration

- [x] 1.1 Add `SlugHelper.ResolveSlugOrDefault` (pure) with unit tests covering blank→name, normalization, and empty→error, verified by `dotnet test --filter SlugHelperTests` green
- [x] 1.2 Add nullable `Slug` to `Product` and `Category` with filtered unique indexes plus a backfill migration (`-2` suffixing, `producto-{id}`/`categoria-{id}` fallback), verified by applying the migration on a scratch database and inspecting slug uniqueness
- [x] 1.3 Reuse the helper in keyword/product/category admin upserts with per-entity uniqueness errors, verified by upserting duplicate slugs and observing localized rejection

## 2. Storefront URLs + canonical redirects

- [x] 2.1 Add the `Details/{productId:int}/{slug?}` route and `cslug` handling on `Search`, with mismatch-301 and absent-slug-200 behavior in both cultures, verified by route/integration tests asserting 200 / 301 / 404 cases
- [x] 2.2 Update all 19 storefront product links plus category chips and the pager to render canonical slug URLs, verified by grep audit (no bare `asp-route-productId` without slug) and rendered-HTML assertions
- [x] 2.3 Fix the 2 deferred CS8601 warnings (`HomeIndexVM.cs:24`, `HomeController.cs:110`), verified by a warning-free build

## 3. SEO + verification

- [x] 3.1 Update sitemap to canonical id-plus-slug URLs and add matching `<link rel="canonical">` on product/category pages, verified by `sitemap.xml` assertions and canonical-tag tests
- [x] 3.2 Run `dotnet build VaultShop.sln` and full `dotnet test VaultShop.sln` green, and manually verify 200/301/sitemap behavior at 320/1280px in es-AR and en-US
