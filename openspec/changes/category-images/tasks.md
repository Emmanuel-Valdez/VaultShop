## 1. Data model

- [ ] 1.1 Add six image columns to `VaultShop.Models/Category.cs` (`ImageUrl`, `ObjectKey`, `FileName`, `ContentType`, `SizeBytes`, `StorageProvider`) and verify `dotnet build VaultShop.sln` succeeds
- [ ] 1.2 Create the EF migration for the new `Categories` columns and verify the migration compiles and applies cleanly on a fresh database

## 2. Shared Skia extraction

- [ ] 2.1 Extract the shared decode-with-orientation / resize / JPEG-75 helper into a new `SkiaImageProcessor` (or equivalent) and verify it is consumed by `ProductImageService` with existing `ProductImageServiceTests` still green
- [ ] 2.2 Refactor `KeywordImageService` to use the shared helper and verify `KeywordImageServiceTests` + `KeywordControllerImageTests` still green
- [ ] 2.3 Verify no private decode/orient/resize copy remains in either service (grep confirms single implementation)

## 3. Category image service + admin controller

- [ ] 3.1 Add `ICategoryImageService` (400px center-square chip) + `CategoryImageValidationException` reusing the shared helper and `.resx` keys, register in `Program.cs`, and verify the service saves a JPEG under `categories/category-{id}`
- [ ] 3.2 Extend `CategoryController.Upsert` to `Upsert(Category obj, IFormFile? imageFile)` with save-new-then-delete-old replace ordering and verify a `CategoryControllerImageTests` case covers replace ordering (old kept when new save fails)
- [ ] 3.3 Add `POST DeleteImage(int categoryId)` clearing the image columns then best-effort deleting the storage object, and verify a test covers db-consistent-then-storage ordering
- [ ] 3.4 Extend category soft-delete to remove the image storage object (best-effort) and verify a test covers it
- [ ] 3.5 Add es-AR + en-US resx keys (upload help, replace/delete, image-error messages) and verify the upsert renders localized strings in both cultures

## 4. Admin upsert view

- [ ] 4.1 Add `enctype="multipart/form-data"`, image file input with current-image preview + replace/delete affordances to `Views/Category/Upsert.cshtml`, and verify via browser that upload/replace/delete round-trip and update the preview

## 5. Storefront pill UI

- [ ] 5.1 Add a shared `_CategoryPill` partial (thumb or first-letter fallback, `aria-hidden` image, active flag) reuse `categoryId` link semantics, and verify it renders in both `Index.cshtml` and `Search.cshtml`
- [ ] 5.2 Add compact `.category-pill` styles to `site.css` (thumb size, gap, label) and verify home + search pills show thumbnail or fallback in `es-AR` and responsive (mobile) views

## 6. Final verification

- [ ] 6.1 Run `dotnet test VaultShop.sln` and verify the full suite is green (293 baseline + new category-image tests)
- [ ] 6.2 Browser-check the full admin upload → storefront render path (upload image, see pill thumb on home + search, delete image, confirm fallback returns) including dark mode