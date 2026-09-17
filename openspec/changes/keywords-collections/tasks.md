## 1. Storage generalization (reuse, no duplication — lands first to de-risk)

- [x] 1.1 Generalize `IImageStorageService`: replace `SaveProductImageAsync(ImageStorageSaveRequest)` / `DeleteProductImageAsync(ProductImage)` with prefix-based `SaveObjectAsync` / `DeleteObjectAsync` (request records carry `Prefix`, `ObjectKey`, `StorageProvider`, `ExpectedPrefix`); keep product key shapes identical (`products/product-{id}/…`, `images/products/product-{id}/…`). Verify: `dotnet build VaultShop.sln` succeeds with the renamed contracts.
- [x] 1.2 Update `MinioImageStorageService` and `LocalImageStorageService` to the new signatures (parameterized prefix + provider/prefix guard on delete, preserving existing anti-traversal checks). Verify: existing `LocalImageStorageServiceTests` and `MinioImageStorageServiceTests` pass with the adjusted signatures.
- [x] 1.3 Update callers: `ProductImageService.SaveProductImagesAsync` (key prefix `products/product-{productId}`) and `ProductController.DeleteImage` (ExpectedPrefix `products/`). Verify: `dotnet test VaultShop.sln` is green after the refactor (ProductControllerUpsertTests, ProductImageServiceTests, storage tests).

## 2. Data model and repositories

- [x] 2.1 Add `Keyword`, `ProductKeyword`, `KeywordImage` (+ `KeywordImageKind` enum Chip/Cover) to `VaultShop.Models` following `Category`/`ProductImage` conventions, with `Keyword.CategoryId` as a bare nullable `int?` (no FK/nav, commented as future placeholder). Verify: project compiles; model review matches design D1.
- [x] 2.2 Add `Product.Keywords` navigation (`List<ProductKeyword>`) and register `DbSet<Keyword>`, `DbSet<ProductKeyword>`, `DbSet<KeywordImage>` in `ApplicationDbContext`. Verify: `dotnet build` passes.
- [x] 2.3 Configure `OnModelCreating`: composite PK `(ProductId, KeywordId)` for `ProductKeyword` (duplicates impossible), non-clustered index on `KeywordId`, unique composite index `(KeywordId, Kind)` for `KeywordImage`, and partial unique index on `Keyword.Slug` with `HasFilter("\"IsDeleted\" = false")`. Verify: model snapshot reflects all four constraints after migration scaffolding.
- [x] 2.4 Add `IKeywordRepository`, `IProductKeywordRepository`, `IKeywordImageRepository` + implementations following `ProductImageRepository`, registering them in `UnitOfWork` and `IUnitOfWork`. Verify: `UnitOfWork` exposes the three repos; existing consumers compile.
- [x] 2.5 Scaffold and review migration `AddKeywordCollections` (3 tables + indexes), then apply. Verify: `dotnet ef migrations add AddKeywordCollections` succeeds, generated SQL contains the composite PK and partial unique slug index, and `database update` applies cleanly on the dev Postgres.

## 3. Slug helper

- [x] 3.1 Add static `SlugHelper.Slugify` in `VaultShop.Utility` (lowercase, ASCII-fold diacritics, non-alphanumeric → `-`, collapse/trim dashes; stdlib regex only). Verify: unit test covers `Naruto` → `naruto`, `Studio Ghibli` → `studio-ghibli`, `My Hero Academia` → `my-hero-academia`, accented input, duplicate runs of separators.

## 4. Admin CRUD (Keyword)

- [x] 4.1 Implement `KeywordController` in Admin area (`[Authorize(Roles = Admin,Employee)]`): `Index`, `GetAll` (JSON list with images + product count), `Upsert` GET/POST (auto-slug from `Name` when blank; localized unique-slug validation; save), `Delete` POST (block when active products reference it → `success=false` + count message; else soft-delete + remove `ProductKeyword` + `KeywordImage` rows), and image delete/replace endpoints. Verify: routes follow `CategoryController`; manual POST of duplicate active slug is rejected with a localized error.
- [x] 4.2 Add `Keyword/Index.cshtml` + `keyword.js` (DataTable, SweetAlert confirm, toastr, block message rendered as error toast without reload) and `Keyword/Upsert.cshtml` (Name, Slug, chip + cover uploads with previews) following `Category` and `Product` upsert visuals. Verify: admin can create/edit/list/soft-delete a keyword and sees image previews; deleting a referenced keyword shows the localized block message.
- [x] 4.3 Add localization resx for `KeywordController` and `Keyword` views in `Resources/Areas/Admin/…` es/en (including the delete-block message with count). Verify: admin pages render both es-AR and en-US without untranslated keys.
- [x] 4.4 Add admin nav entry for Keywords/Colecciones in the admin layout. Verify: link renders and routes to `/Admin/Keyword` under both cultures.

## 5. Keyword image handling via storage

- [ ] 5.1 Implement `IKeywordImageService` (SkiaSharp validation + resize, ~400×400 chip / ~1400×500 cover, prefix `keywords/keyword-{id}`) and register in DI (`Program.cs`). Verify: uploading a chip then a cover then replacing the chip persists each independently and stores bytes through `IImageStorageService` (metadata row only in DB); rejects oversized/undecodable/unsupported files with localized errors (mirrors `ProductImageService`).

## 6. Product/Upsert keyword selector

- [ ] 6.1 Extend `ProductVM` with `KeywordList` (SelectListItems) + `SelectedKeywordIds`; populate in `ProductController.Upsert` GET and `PopulateProductFormData`. Verify: edit page lists active keywords as checkboxes and pre-checks the product's current selection.
- [ ] 6.2 Add checkbox group ("Keywords / Colecciones", `form-check` + badges) to `Product/Upsert.cshtml` directly under the Category select. Verify: markup renders a native, keyboard/touch-accessible multi-select distinct from the Category dropdown.
- [ ] 6.3 Sync `ProductKeyword` rows on POST from `SelectedKeywordIds` (add missing / remove extra) before `Save()` for both create and update. Verify: saving a product with multiple keywords persists all links; unchecking removes links; duplicate selection does not create duplicate rows (composite PK safe).

## 7. Storefront — Home collections row + counter

- [ ] 7.1 Extend the product query in `HomeController.Index` to include `ProductKeywords.Keyword.KeywordImages`, then compute the collection list (active keywords reachable from visible products) and per-keyword distinct counter (exclude `StockQuantity == 0`; single in-memory pass over the already-loaded product list). Expose via `HomeIndexVM`. Verify: `Index` loads with 0 extra queries beyond the existing product load; unit test: 26 associated products, 2 with stock 0 → counter shows 24.
- [ ] 7.2 Render the Collections section in `Index.cshtml` below `ShopByCategory` — one horizontal row (CSS `overflow-x:auto`, `scroll-snap`, `flex-wrap:nowrap`), circular chip image or letter fallback, name, optional `(N)` counter, chips link to `Search?keywordId={id}`. Hide section when no collection has ≥1 product. Verify: desktop shows a single row, mobile scrolls horizontally 1 row with touch-friendly targets, no wrap or layout break, section absent when empty; qualifies WCAG (focusable links, `aria-current` on active).
- [ ] 7.3 Add storefront resx strings for the collections heading/name (es-AR/en-US). Verify: heading localized.

## 8. Storefront — Search filters, active state, pagination

- [ ] 8.1 Add `categoryId` and `keywordId` parameters to `HomeController.Search`, AND-combined with the existing text `searchString` filter (filters after the same visibility predicate; keyword match by exact ID via `ProductKeywords`; missing/soft-deleted ids → empty set, no exception). Verify: `keywordId=7` returns only products tagged 7; `keywordId=7&categoryId=2&searchString=negra` returns the AND-intersection; existing singular text-search behavior unchanged; integration test added for the combined filter.
- [ ] 8.2 Change Home category shortcuts (`Index.cshtml`) from `searchString=Category.Name` to `categoryId=Category.Id`. Verify: clicking a category shortcut returns exactly that category's products and no longer collides with products whose text matches the category name.
- [ ] 8.3 Update `_Pager.cshtml` to carry `keywordId` and `categoryId` (`asp-route-*`) alongside `searchString`; confirm Razor omits null values so unfiltered pages emit clean URLs. Verify: page-2 link preserves active `keywordId`/`categoryId`/`searchString`; unset filters produce no empty params.
- [ ] 8.4 Render active-filter chips on `Search.cshtml` (active collection highlighted + `×` link that removes only the keyword filter, keeping category/search; category active-chip similarly removable). Verify: navigating a collection marks the matching chip active, removal returns to results keeping other filters, works on mobile touch.

## 9. SEO guard

- [ ] 9.1 Confirm `SeoController.Sitemap` gains no keyword URLs (no code change expected). Verify: sitemap output unchanged (static pages + product details only) after keywords exist in the DB.

## 10. Integration verification

- [ ] 10.1 Full end-to-end: create keyword + chip + cover in admin, assign to 2 products in different categories, browse store → collection row shows chips + counts, click → filtered results, combine with category + text, paginate preserving filters, remove filter. Verify: `dotnet test VaultShop.sln` green + manual store/admin/mobile sweep (per design Migration Plan step 5).
- [ ] 10.2 Run lint/build cleanups and remove any dead code introduced during apply; confirm no spec'd behavior is missing. Verify: build warnings cleared and final `git status` includes only intended files.