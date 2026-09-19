## Context

See `proposal.md` for the what/why. Constraining current state:

- `Category` (`VaultShop.Models/Category.cs`) is a bare EF entity (`Id, Name, IsDeleted, AvgShippingCost`, plus `PackagingByCategory` nav) with no image support. `CategoryController` (admin) is sync, binds `Category obj` directly, `[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]`.
- Two image pipelines exist: `ProductImageService` (multi-image, primary/sort) and `KeywordImageService` (chip 400² square-crop + cover 1400x500). Both duplicate decode-with-orientation + resize + JPEG-75 logic; `KeywordImageService.cs:126` explicitly marks it: "extract to a shared Skia helper if a third image type appears."
- Storefront categories are derived on the fly from products (`HomeController.cs:49` Home, `:291` Search): `productList` is eager-loaded with `Category` and the pill row is built from `g.First().Category`. Any image columns on `Category` surface here with zero query changes.
- Keyword images use object paths `keywords/keyword-{id}`; keyword soft-delete removes image storage objects (best-effort cleanup) in `KeywordController.cs:188`.

## Goals / Non-Goals

**Goals:**
- Single image per category, stored on the `Category` row (flat, no child entity).
- Admin upload/replace/delete in the category upsert, mirroring keyword image UX.
- Compact thumbnail pills on Home + Search with first-letter fallback.
- Honor the recorded Skia-dedup promise (third image type triggers extraction).

**Non-Goals:**
- Category covers/heroes (keyword-cover analogue). If needed later, add columns then.
- Multiple images or carousels per category.
- Admin category-list thumbnail column or DataTable changes.
- Image backfill for existing categories (fallback covers them).

## Decisions

### D1 — Flat image columns on `Category` (not a `CategoryImage` child table)

Six columns mirroring a single `ProductImage`/`KeywordImage` row's storage metadata: `ImageUrl`, `ObjectKey`, `FileName`, `ContentType`, `SizeBytes`, `StorageProvider`.

- Why: exactly one image per category with no kind/sort/primary axes — the child-table motivation that drives `ProductImage` (1:N) and `KeywordImage` (1:2, `Kind` enum) does not exist here. Flat columns also travel through the storefront's existing `Product.Category` eager-load for free; a child table would require `includeProperties` (or a relationship config) on every category query, including the Home/Search derivation paths.
- Alternatives: `CategoryImage` child table (mirrors existing patterns; rejected as heavier than the need and adds query friction); single `ImageUrl` column only (rejected: loses `ObjectKey`/`StorageProvider` needed for `DeleteObjectAsync`, and diverges from the established storage-metadata convention).

### D2 — Extract shared Skia image processing for the third consumer

A small internal helper (e.g. `SkiaImageProcessor`) owning validation, decode-with-orientation, center-square-crop/contain resize, and JPEG-75 encode. `ProductImageService` and `KeywordImageService` are refactored to consume it; the new `ICategoryImageService` (thin, mirrored after the two existing I+impl pairs) uses it for a 400px center-square crop.

- Why: this is the exact event the recorded `ponytail:` comment deferred for. A third copy (~80 lines × 3) is more total code than one helper + three thin consumers, and keeps image behavior in one place.
- Alternatives: third private duplication (smallest diff now, but breaks the recorded plan and triples the hot-path surface); categorize into a generic `IImageProcessingService` with no per-entity wrapper (diverges from the codebase's I+impl-per-feature convention).

Validation errors stay localized per service via the existing `*ValidationException` pattern (`KeywordImageValidationException`); category gets its own exception type so the localized messages bind correctly.

### D3 — Category-scoped image endpoints (no image row id)

Because image data lives on `Category`, the admin actions are category-scoped, unlike `KeywordController.DeleteImage(int imageId)`:

- `POST Upsert(Category obj, IFormFile? imageFile)` — `enctype="multipart/form-data"` on the form.
- `POST DeleteImage(int categoryId)` — clears the six columns, then best-effort deletes the storage object (mirrors keyword cleanup ordering: DB-consistent first, storage second).
- Uploads use `categories/category-{id}` as the object path.

Replace-as-upload keeps the keyword ordering guarantee: save new → persist DB → delete old (best-effort). A validation failure returns the upsert view with the localized error and the category untouched.

### D4 — Shared storefront pill partial

A small partial (e.g. `_CategoryPill`) rendered by both `Index.cshtml` and `Search.cshtml`, accepting the category + active flag. Preserves the existing pill classes and `categoryId` link semantics (`catalog` spec requirements about filter navigation are untouched); adds a `<span class="category-pill__image" aria-hidden="true">` holding either the thumb or the uppercased first-letter fallback (reusing the keyword contact-letter approach), followed by the existing label.

- Why: two call sites with one screen's worth of shared markup; the partial is the boundary, and it keeps `aria-current` handling in the caller where the active state differs (Home has none).
- Alternative: duplicate the markup in both views (rejected: two copies drift, and the fallback/thumb branching is exactly the kind of thing a partial centralizes).

### D5 — Compact CSS, not the big chip look

New `.category-pill` styles in `site.css` (small rounded pill, ~28px thumb, gap, label) rather than reusing the hero-scale `collection-chip__*` classes. The keyword row stays the visual hero; categories remain the compact secondary filter per the decided UX.

### D6 — DI, localization, tests

- `ICategoryImageService` + `IImageStorageService` registered in `Program.cs`; `CategoryController` gains the DI params (it currently has none beyond `IUnitOfWork`/localizer).
- New es-AR + en-US keys on the category upsert resx: upload help, replace/delete affordances, and image-error messages (empty/too-large/invalid-extension/content-type/not-an-image shared via the existing `UploadFile*` keys where applicable).
- Tests mirror `KeywordImageServiceTests` + `KeywordControllerImageTests`: validation rejection, square-crop normalization, replace ordering (old kept on new-failure), delete clears columns + storage, soft-delete storage cleanup, and storefront fallback thumb rendering.

## Risks / Trade-offs

- **Soft-delete destroys the image** (`IsDeleted` is reversible, but the stored object is deleted on delete). → Mirrors existing keyword behavior deliberately; restoring a category reverts to the letter fallback. Acceptable per product decision.
- **Skia extraction touches two existing services** → Regression surface mid-feature; mitigated by the existing `KeywordImageServiceTests`/`ProductImageServiceTests` staying green and the new helper being behavior-identical (a refactor, not a redesign).
- **Flat columns diverge from child-table precedent** → Acceptable per D1; the six-column convention keeps storage metadata uniform even though the row lives on `Category`.
- **`Upsert(Category obj, IFormFile?)` binds the EF entity** → existing pattern (`KeywordController.Upsert(Keyword obj, IFormFile? ...)`) is already in place; no viewmodel refactor this change.

## Migration Plan

- Additive EF migration adding the six nullable columns to `Categories`. No backfill; existing rows rely on the fallback. Deploy order: migration + code together as today (single-container release). Rollback: feature is additive; downgrading the migration drops the columns and any uploaded images, which is acceptable for a fresh, non-backfilled feature.