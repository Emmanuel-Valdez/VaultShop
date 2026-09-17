## Context

See proposal.md — Why. Current state that shapes the implementation:

- **Category is a single flat taxonomy** (`Category { Id, Name, AvgShippingCost, IsDeleted }`, `VaultShop.Models/Category.cs`). One product → one category (`Product.CategoryId`). Category carries packaging + shipping cost logic (`AvgShippingCost`, `PackagingByCategory`) that SHALL NOT be touched.
- **Visibility rule repeated inline** across controllers: `!IsDeleted && IsAvailableInStore` (e.g. `HomeController.cs:41`, `FavoriteController.cs:34`). There is **no shared "visible product" abstraction** — the codebase re-declares the predicate per query. Stock is surfaced in the storefront as an out-of-stock badge (`stock-visibility`); the counter additionally needs `StockQuantity > 0`.
- **Search today** (`HomeController.Search`) loads all visible products, then filters in-memory with `CompareInfo.IndexOf` (accent/case-insensitive) over `Name`, `Category?.Name`, `Description`. Category shortcuts on the home page are actually `searchString=Category.Name` (a text match, not an ID). Pagination (`_Pager.cshtml`) preserves **only** `searchString` — `keywordId`/`categoryId` will need adding.
- **Repo/UnitOfWork pattern**: `Repository<T>` with `Get/GetAll/Add/Remove/RemoveRange` + per-entity repos (`ProductImageRepository`, `CategoryRepository`, ...) wired in `UnitOfWork`. `Save()` = one `SaveChanges`. No raw `_db` access from controllers (except `SeoController`).
- **Image storage is Product-specific**: `IImageStorageService` exposes `SaveProductImageAsync(ImageStorageSaveRequest{ ProductId, ... })` and `DeleteProductImageAsync(ProductImage)`. Implementations hard-code key prefixes: MinIO `products/product-{id}/{guid: N}.jpg` (`MinioImageStorageService.cs:31-32`), Local `images/products/product-{id}/{guid}.jpg` (`LocalImageStorageService.cs:22-24`). Delete validates the `StorageProvider` + object-key prefix before removing (`ShouldHandleImage`, `GetSafeRelativeImagePath`). `ProductImageService` (SkiaSharp resize → 1000×1200 JPEG) mirrors this at the entity level.
- **No carousel/Tom Select/other UI libraries**: wwwroot has bootstrap, datatables, jquery, validation, quill only. Horizontal scroll must be plain CSS (`overflow-x` + `scroll-snap`); multi-select on Product/Upsert must be checkboxes, not a new lib.
- **Admin CRUD pattern** (Category Reference): `Index.cshtml` + `/js/<name>.js` DataTable with `ajax` → `GetAll()`, `Upsert` GET/POST, `Delete` POST returning `{ success, message }` JSON, SweetAlert confirm. Localization via `Resources/Areas/Admin/Controllers/<Name>Controller.{es,en}.resx` and `Resources/Areas/Admin/Views/<Name>/{Index,Upsert}.{es,en}.resx`.
- **Sitemap** (`SeoController.Sitemap`) emits static pages + `Home/Details/{id}` only; no keyword URLs SHALL be added.
- **No existing Slug code** anywhere in the repo.

## Goals / Non-Goals

**Goals:**
- Add `Keyword` + M:N `ProductKeyword` with composite PK and a partial unique slug index over active keywords, fully decoupled from Category pricing/packaging/hierarchy.
- Generalize the existing `IImageStorageService` to a prefix-based save/delete so `ProductImage`, keyword chip/cover images, and the upcoming category-image spec share one storage layer — no parallel storage architecture.
- Keyword admin CRUD (list/create/edit/soft-delete, two optional independent images with previews), matching Category conventions.
- Product/Upsert multi-keyword selector (native checkboxes, no new dependency).
- Storefront collections row near ShopByCategory: horizontal single-row scroll, circular chips, fallback letter chip, active state + remove, optional distinct counter excluded for stock-0.
- Exact `keywordId` filter AND-combined with `categoryId` + `searchString`; category shortcuts move to ID routing; `_Pager` preserves all three.

**Non-Goals:**
- No category hierarchy, `ParentCategoryId`, or Category→Keyword gating.
- No per-keyword image filesystem `/SAAS`-style; images stay in the existing MinIO/Local abstraction.
- No keyword landing pages (`/coleccion/{slug}`), SEO, sitemap URLs, or editorial content in v1 — slug is stored but unused.
- No multiple-keyword OR filtering, no JSON/Postgres-array tags.
- No packaging/cost/pricing inheritance from Keyword, no `Category` model or pricing changes.
- No cache layer (IMemoryCache) in v1 — the aggregate counter query is cheap at this catalog size.

## Decisions

### D1 — Schema: `Keyword`, `ProductKeyword`, `KeywordImage`
Models under `VaultShop.Models`:

```
Keyword        { Id, Name, Slug, IsDeleted, CategoryId? /* reserved, no FK/nav */,
                 List<KeywordImage> Images, List<ProductKeyword> ProductKeywords }
ProductKeyword { ProductId, KeywordId }            // composite PK (ProductId, KeywordId)
KeywordImage   { Id, Kind /* Chip | Cover */, ImageUrl, ObjectKey, FileName, ContentType,
                 SizeBytes, StorageProvider, CreatedAtUtc, KeywordId }
```

- `Keyword.CategoryId` is a bare `int?` column — **no FK, no navigation, no admin field, no validation** — documented as a future-evolution placeholder per spec. Rationale: a plain nullable column has zero coupling; adding an FK would only introduce constraints we must not exercise in v1.
- `ProductKeyword` composite PK **is** the uniqueness constraint (duplicates impossible at DB level). Add a secondary index on `KeywordId` for reverse lookups (counter aggregates, delete-guard) since the PK leads on `ProductId`. EF fluent config mirrors existing style in `ApplicationDbContext.OnModelCreating`.
- `KeywordImage.Kind` is an enum (Chip/Cover); uniqueness enforced via a composite unique index `(KeywordId, Kind)` — a keyword can have at most one of each. Mirrors `ProductImage` metadata shape (`ImageUrl/ObjectKey/...`), minus `SortOrder`/`IsPrimary` which are product-gallery concerns, not collection concerns. Rationale: reuses the proven `ProductImage` storage pattern; the next category-images spec will use the same generalized storage + a near-identical image entity.
- **Alternative considered**: two nullable column pairs on `Keyword` (chip + cover URL/key/...). Rejected: adds 10 nullable columns, duplicates storage metadata shape, and diverges from the `ProductImage` pattern the user asked to follow.

### D2 — Storage generalization (reuse, not duplication)
Refactor `IImageStorageService` from product-specific to prefix-based. The interface changes to:

```csharp
Task<StoredImage> SaveObjectAsync(ImageStorageSaveRequest request, ...);   // request: { Prefix, Content, FileName, ContentType, SizeBytes }
Task DeleteObjectAsync(DeleteObjectRequest request, ...);                  // request: { ObjectKey, StorageProvider, ExpectedPrefix }
```

- Replace `ProductId` in `ImageStorageSaveRequest` with `Prefix` (string, e.g. `products/product-7` or `keywords/keyword-3`). Object keys become `{Prefix}/{guid}.jpg` — MinIO unchanged in shape, Local path becomes `images/{Prefix}/{guid}.jpg`.
- Delete takes a raw `ObjectKey` + `StorageProvider` + allowed `Prefix`, replacing the `ProductImage`-typed overload; both implementations keep their existing anti-traversal + provider checks but parameterized by `ExpectedPrefix`.
- Callers updated: `ProductImageService` (save), `ProductController.DeleteImage` (delete), and the new `KeywordImageService`/`KeywordController` (save/delete). `Program.cs` registration unchanged (`AddScoped<IImageStorageService, ...>` by provider switch).
- **Alternative considered**: new sibling interfaces/services per entity. Rejected — that is exactly the parallel architecture the user forbade; prefix-based generalization is the smallest change that keeps one storage layer for product + keyword + category images.

### D3 — Keyword image handling service
`KeywordImageService` (or methods on a shared helper) handling upload → validate (reuse `ProductImageService` validation semantics: allowed extensions/content-types, ≤10 MB, decodable via SkiaSharp) → resize → `SaveObjectAsync` with prefix `keywords/keyword-{id}`. Two helpers: `SaveChipAsync` (square crop, e.g. 400×400; chips render circular) and `SaveCoverAsync` (wide, e.g. 1400×500). Files go to `IImageStorageService`; metadata rows go through `IKeywordImageRepository`. Delete mirrors `ProductController.DeleteImage` (remove row, best-effort storage delete with provider-prefix guard).
- Reuses SkiaSharp (already referenced by `ProductImageService`). No new package.
- **Alternative**: skip resizing and store originals. Rejected: `ProductImageService` already establishes the resize-on-upload convention; chips/cover have strong dimension expectations and originals would bloat MinIO/local webroot.

### D4 — Slug derivation + unique partial index
- No repo precedent for slugs (all deferred backlog). Write a tiny, static friendly-slug helper (e.g. `VaultShop.Utility.SlugHelper.Slugify(string)`) — lowercase, ASCII-fold (diacritics → base letters, `á`→`a`), non-alphanumeric → `-`, collapse/trim `-`. No new package (`System.Text.RegularExpressions` built-in).
- Created keywords without a slug get `Slugify(Name)` automatically. Uniqueness among **active** keywords enforced by a partial unique index in `OnModelCreating`:

```csharp
modelBuilder.Entity<Keyword>()
    .HasIndex(k => k.Slug)
    .IsUnique()
    .HasFilter("\"IsDeleted\" = false");
```

  EF/Npgsql emits the partial index verbatim; a soft-deleted `naruto` does not block a new `naruto`. Admin slug edit validated for the active-unique rule with a localized error.

### D5 — Soft delete + delete guard (Category semantics)
`KeywordController.Delete(id)` mirrors `CategoryController.Delete`:
1. load `!IsDeleted` keyword → 404/error JSON if missing;
2. count `ProductKeyword` rows referencing **active** products (`!Product.IsDeleted`); if > 0 → `{ success=false, message = _localizer["DeleteBlockedHasProducts", count] }` and no change;
3. else set `IsDeleted = true`, remove the keyword's `ProductKeyword` rows + delete its image rows, `Save()` in one unit (nested transactions not needed — a single `SaveChanges` covers the FK-less dependent rows) → `{ success=true, message }`.
- Rationale: matches the existing category flow (`CategoryController.cs:96-115`) and the `catalog` deletion-guard spec; keyword images are FK child rows removed with it (storage objects best-effort deleted by key).
- **Note**: product soft-delete (`ProductController.Delete`) currently leaves `ProductKeyword` rows attached (dangling but harmless – the join is never surfaced for deleted products). Matches how `ProductCategory`-equivalent refs already behave; no cleanup required in v1.

### D6 — Product/Upsert multi-select (checkboxes, no new lib)
`ProductVM` gains `List<SelectListItem> KeywordList` + `List<int> SelectedKeywordIds`. Upsert renders a checkbox group ("Keywords / Colecciones") directly under the Category select, distinct visually (`form-check` + badges). POST persists by diffing `SelectedKeywordIds` vs existing `ProductKeyword` rows (add missing, remove extra) before `Save()`. New product: insert rows for all selected.
- **Alternative considered**: Tom Select. **Not present in the project** (`wwwroot/lib` has no such package) — adding it violates the no-new-dependency rule when native checkboxes cover the need and are more accessible (keyboard/touch, matches Bootstrap `form-check` used elsewhere).

### D7 — Storefront collections row (Index + Search active state)
- `HomeController.Index`: after loading the existing visible `productList` (already loaded for pagination — no extra query for products), compute collections from the **same in-memory list**: distinct `Keywords` reachable through `Product.ProductKeywords`. Load `Keywords` + `ProductKeywords` + keyword images via `includeProperties` on the product query (`"Category,ProductImages,ProductKeywords.Keyword.KeywordImages"`).
- Counter = distinct products per keyword from that list filtered by `StockQuantity > 0` (the list already guarantees `!IsDeleted && IsAvailableInStore`). One pass, zero N+1, no cache. Rationale: at current catalog size the index already materializes all visible products (in-memory pagination at `HomeController.cs:40-56`); piggy-backing counts on that list is the cheapest correct approach. If the catalog grows, the fixed step is to push filtering/counting to SQL (spec scenario "Counts computed without N+1" holds either way — it's one aggregate, not per-keyword queries).
- Render a new `<section id="colecciones">` under the `ShopByCategory` block in `Index.cshtml`: heading "Explorá por colección" + a `div.collections-row` (CSS: `display:flex; overflow-x:auto; scroll-snap-type:x proximity;` on desktop and mobile; **no wrap** — `flex-wrap:nowrap`). Each chip: `<a asp-action="Search" asp-route-keywordId="@k.Id">` with a circular image or letter fallback + name + optional `(N)`. Hide the whole section when no keyword has ≥1 product (per spec).
- `Search.cshtml`: read `keywordId`/`categoryId` from query; render active-filter chips with `×` links that drop only that filter while keeping the others.
- **Active chip**: when `Search` has `keywordId` matching a collection, the corresponding chip in the collections section (and/or an active-filter chip at top of results) gets `aria-current` + `.active` styling + a `×` to clear. Keeps one navigation mental model.
- **Alternative considered**: merge Categories + Collections into one "Shop by" strip. Rejected: the user asked to prioritize clarity (two dimensions, different language; see proposal — Objectives). Independent, separately-labeled rows under the same storefront section avoid mixing "types of products" (Category) with "themes/universes" (Collection).

### D8 — Search filter semantics + pagination
- `HomeController.Search(string searchString, int? categoryId, int? keywordId, int pageNumber)`:
  1. base visible products (same predicate);
  2. `if (categoryId.HasValue)` → keep `p.CategoryId == categoryId`;
  3. `if (keywordId.HasValue)` → keep products whose `ProductKeywords.Select(k => k.KeywordId).Contains(keywordId.Value)`;
  4. text `searchString` filter unchanged (`CompareInfo.IndexOf`), AND-combined.
- Home category shortcuts (`Index.cshtml:97`) change from `asp-route-searchString="@category.Name"` to `asp-route-categoryId="@category.Id"` (and show category name as the search term heading when no text). This satisfies the catalog spec requirement "Home category shortcuts use category id" and prevents category-name text collisions.
- `_Pager.cshtml`: include `asp-route-keywordId`, `asp-route-categoryId` alongside `searchString` on every link (Razor automatically omits null route values, so unset filters produce clean URLs). Verified current `_Pager` only carries `searchString` (lines 4-5, 13/20/25) — this is the required change.
- Missing/soft-deleted keyword/category ids → empty result set (no crash): treating the filter as "no match" is the simplest correct behavior and matches the spec scenario.

### D9 — Admin navigation & localization
- Add a `Keywords`/`Colecciones` entry to the admin nav (`_Layout` admin section) linking `@{culture}/Admin/Keyword` — same pattern as Category links.
- Resources: `Resources/Areas/Admin/Controllers/KeywordController.{es,en}.resx`, `Resources/Areas/Admin/Views/Keyword/{Index,Upsert}.{es,en}.resx`, plus storefront strings in the Customer `Home` views' resx. Follows the exact existing layout.
- `keywords.js` mirrors `category.js`/`product.js` (DataTable + Ajax CRUD + SweetAlert confirm + toastr), with the delete guard rendering the server's `success=false` message as an error toast and not reloading (as done for category blocks).

### D10 — Data migration approach
One EF Core migration (e.g. `AddKeywordCollections`): creates `Keywords`, `ProductKeywords` (composite PK + KeywordId index), `KeywordImages` (Kind enum → int + unique `(KeywordId, Kind)` index + partial unique `Slug` filter index). No backfill needed (new tables, empty). Rollback = revert migration (`dotnet ef migrations remove` before deploy, or drop tables via `Script-Migration` in prod — same as existing migrations). No app-config/env changes.

## Risks / Trade-offs

- **Storage interface change touches tested code** (MinIO + Local storage, product image flow, `ProductControllerUpsertTests`, `LocalImageStorageServiceTests`, `MinioImageStorageServiceTests`) → keep the refactor mechanical (rename/signature only, same key shapes for products); run `dotnet test` after the refactor and again after keyword features. Small diff, high reuse payoff.
- **Counter correctness depends on replicating the visibility predicate** (no shared abstraction exists) → keep the predicate copied exactly as today (`!IsDeleted && IsAvailableInStore`, + stock>0) in one place (Index) and add a focused unit test for the count rule (26 associated / 2 stock-0 → 24).
- **Native checkbox multi-select UX** is plainer than a tag picker → acceptable for v1; a future keyword-set can swap in a picker without schema change (selection is a plain id list on the VM).
- **Partial unique index is applied by Npgsql via `HasFilter`** — some DB providers differ; project is Postgres-only (confirmed), so `"IsDeleted" = false` filter syntax is portable across the two store environments (platform + store compose use Postgres).
- **Slug uniquess enforced at DB + admin-name duplication** — `Name` is NOT unique (two collections could share a name with different slugs). Intentional: slugs are the identity for future URLs; name is display-only.
- **Best-effort storage deletion on keyword delete** mirrors `ProductController.DeleteImage` (row first, log-and-continue on storage failure) — a rare orphaned object in MinIO/webroot is acceptable and consistent with existing behavior.

## Migration Plan

1. Storage generalization (D2) lands first as a standalone mechanical change with existing tests green — decouples it from keyword feature work and de-risks the interface change.
2. Then schema migration (D10) + models + repositories.
3. Then admin CRUD + images, then Product/Upsert selector.
4. Then storefront (Index row, counter, Search filters, active state, `_Pager`).
5. `dotnet test VaultShop.sln` full pass; manual browse of store + admin + mobile viewport.
6. Rollback: revert the storage-generalization commit, `dotnet ef migrations remove` (pre-deploy) or a down script (post-deploy); no data to lose (new tables).

## Open Questions

None blocking v1. (Flagged for follow-up, not blockers: whether the counter stays always-visible or becomes a config toggle; whether chip vs cover aspect ratios need brand-specific values — both are safe to tune post-apply without spec changes.)