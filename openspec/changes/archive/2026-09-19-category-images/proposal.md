## Why

Categories are the storefront's primary browse entry point but render as bare text pills. A representative thumbnail per category makes the "Shop by Category" filters scannable and visually anchors each taxonomy group, matching the polish level the keyword collections already have.

## What Changes

- **Category model** gains six flat image columns (`ImageUrl`, `ObjectKey`, `FileName`, `ContentType`, `SizeBytes`, `StorageProvider`) + EF migration. One image per category, no child table.
- **Admin category upsert** accepts a single image upload (`IFormFile`) alongside name/shipping cost; replaces the existing image (save-new-then-delete-old) and exposes a delete-image action. Uploaded images are validated (extension/content-type/size/decode), square-cropped to ~400px and re-encoded as JPEG via SkiaSharp, stored under `categories/category-{id}`.
- **Storefront category filters** (Home "Shop by Category" and Search "Shop by Category") keep their compact pill style but gain a small rounded thumbnail before the label. Categories without an image fall back to a first-letter avatar.
- **Skia helpers deduplication** (previously marked in `KeywordImageService.cs` as "extract if a third image type appears"): shared decode-orient-resize logic extracted, `ProductImageService` + `KeywordImageService` + new category service all consume it.
- **Category soft-delete** also deletes the image storage object (best-effort), mirroring keyword deletion.

## Capabilities

### New Capabilities
- `catalog/category-images`: storefront category filter thumbnails (with first-letter fallback) and admin category image management (upload, replace, delete, storage cleanup).

### Modified Capabilities
<!-- No requirement changes to existing specs: the category filter navigation semantics in `catalog` are unchanged; this only adds a visual to already-rendered pills. -->

## Impact

- `VaultShop.Models/Category.cs` — 6 new columns.
- `VaultShop.DataAccess` — new migration; `ApplicationDbContext` model config.
- `VaultShop.Web/Areas/Admin/Controllers/CategoryController.cs` — upload/replace/delete endpoints, storage-aware soft-delete.
- `VaultShop.Web/Areas/Admin/Views/Category/Upsert.cshtml` — `multipart/form-data`, image field with preview.
- `VaultShop.Web/Areas/Customer/Views/Home/Index.cshtml` + `Search.cshtml` — pill thumbnail rendering.
- New `ICategoryImageService` + shared Skia helper; `Program.cs` DI registration.
- `VaultShop.Web` resource files (es-AR + en-US) — new localization keys.
- `VaultShop.Tests` — category image service and controller tests mirroring keyword image tests.
- Storage: MinIO/local `categories/category-{id}` object paths. Existing categories have no image and rely on the fallback; no backfill.