## Why

Products today are organized under a single flat `Category` ("Mochilas", "Remeras") that answers "what kind of product is this?". The store needs a second, transversal discovery dimension — "Keywords" presented to shoppers as **Collections/Temáticas** ("Naruto", "Berserk", "One Piece", "Studio Ghibli") — so a product in one category can carry multiple thematic tags, and the storefront gets a visual, clickable entry point that drives product discovery. The model must stay clean enough to later evolve into full collection landing pages (slug already planned) without coupling to Category today.

## What Changes

- **New `Keyword` entity**: `Id`, `Name`, `Slug`, `IsDeleted` (soft delete), and a nullable `CategoryId` **reserved only as an architectural placeholder for a future Category→Keyword relationship**. The reserved column has no FK, no navigation, no validation, filtering, or admin behavior in v1. Category remains fully untouched (no hierarchy, no `ParentCategoryId`, no pricing/packaging changes).
- **New M:N `ProductKeyword` join table**: `ProductId` + `KeywordId` composite primary key (prevents duplicates by construction), with an index on `KeywordId` for reverse lookups.
- **Slug uniqueness among active keywords**: unique index on `Slug` (PostgreSQL partial index `WHERE "IsDeleted" = false`), so a soft-deleted keyword does not block reusing its slug. Slug is editable in admin and is created from `Name` when empty.
- **Keyword images via the existing storage architecture**: two independent, optional images per keyword — **chip** (square, shown circular in the storefront) and **cover** (collection hero). Bytes go to MinIO/Local via the existing `IImageStorageService`; DB keeps metadata only. The storage layer is generalized (entity-agnostic object key prefix, generic save/delete by key) so the same infrastructure is reused by this and the upcoming category-images spec. No parallel storage architecture.
- **Admin CRUD for Keywords** following the Category/product pattern: DataTable list (JS + DataTables), Upsert view with `Name`, `Slug`, chip image, cover image (with previews), soft delete guarded when active products reference it. Roles: Admin + Employee, identical to Category.
- **Product/Upsert multi-keyword selector** grouped near the Category dropdown, using the native checkbox/select-multi pattern already available (no new JS library; Tom Select is not present in the app).
- **Storefront Collections section** on Home/index, positioned near `ShopByCategory` as its own clearly labeled row ("Explorá por colección"): one horizontal row of circular chip images with the keyword name below, image fallback (letter chip), optional product counter, no divergent visual system. On mobile: single touch-friendly row with horizontal scroll, never wrapping into multiple rows.
- **Exact keyword filtering by ID** (`?keywordId=N`), AND-combined with the existing `searchString` and the category filter. Existing text search semantics are preserved; textual search is never used as the keyword filter mechanism. A `categoryId` filter parameter is added so the three filters combine cleanly.
- **Pagination preserves all active filters**: `_Pager` currently only carries `searchString`; it will carry `keywordId`, `categoryId`, and `searchString` on every page link.
- **Active keyword state + removal**: the active collection chip is visually highlighted and offers a clear "remove filter" control (e.g. `×`) that only drops the keyword filter, keeping other filters.
- **Aggregate product counter**: count of **distinct** active products (`IsDeleted == false && IsAvailableInStore == true && StockQuantity > 0`) per keyword, computed in one aggregated pass — no per-keyword N+1 queries, no complex cache in v1.
- **SEO**: keyword URLs are intentionally **not** added to the sitemap in v1; the slug exists for the future `/coleccion/{slug}` landing evolution.

## Capabilities

### New Capabilities
- `catalog/keywords`: transversal Keyword/Collection catalog — model, admin CRUD with two optional images, product assignment, exact filtering combined with category/search, storefront discovery row, counters, slug uniqueness, reserved future CategoryId.

### Modified Capabilities
- `catalog`: the storefront search/catalog behavior gains ID-based keyword and category filters (exact, AND-combined) and filter-preserving pagination, while the existing text search keeps working.

## Impact

- **Models**: `Keyword`, `ProductKeyword`, `KeywordImage` (new); `Product` + `ProductKeywords` navigation; `ProductVM` + keyword selection; `HomeIndexVM` + collections.
- **Data**: one EF Core migration (3 tables, composite key, partial unique slug index, KeywordId index); `ApplicationDbContext` + 3 DbSets.
- **Repositories**: `IKeywordRepository`, `IProductKeywordRepository`, `IKeywordImageRepository` + implementations, wired into `UnitOfWork` (existing pattern).
- **Storage refactor** (reuse, no duplication): `IImageStorageService`, `ImageStorageSaveRequest`, `MinioImageStorageService`, `LocalImageStorageService` generalized from product-specific (`ProductId`, fixed `products/` prefix) to prefix-based save/delete; `ProductController`/`ProductImageService`/storage tests updated to the new signatures. This is what lets the next category-images spec reuse the same layer.
- **Admin**: `KeywordController` + Index/Upsert views + `wwwroot/js/keyword.js` + localized resx (es-AR/en-US).
- **Storefront**: `HomeController` (Index collections + counts; Search gets `keywordId`/`categoryId` AND filters), `Index.cshtml` (collection row), `Search.cshtml` (active filter/removal), `_Pager.cshtml` (preserve filters), `site.css` (chip row + horizontal scroll).
- **Dependencies**: none added (SkiaSharp resize reused; native CSS scroll; checkbox multi-select).
- **Tests**: update existing storage/controller tests for generalized signatures; add keyword slug/counter/filter tests.
- **Explicitly out of scope v1**: category hierarchy (`ParentCategoryId`), Category→Keyword gating, packaging/cost/pricing inheritance from Keyword, keyword landing pages, sitemap keyword URLs, multiple-keyword OR filters, JSON/Postgres-array tags.