## Why

The VaultShop home page has no admin-editable promotional surface: the only banner-like blocks are the hardcoded, localized `home-value-banner` and `home-trust-strip`, and product/collection rows are auto-generated from catalog data. Store owners need to spotlight editorial moments — a new collection, a bank-transfer discount, a seasonal drop — with a purposeful image, a call to action, and copy they control, without a developer deploy.

## What Changes

- Introduce a `Banner` entity (admin-managed, multiple rows, ordered, `IsActive` filtered) and a `BannerImage` child table holding the image set (`Master` / `Medium` / `Small` landscape densities plus an optional `Mobile` art-directed asset), mirroring the existing `KeywordImage` pattern.
- Give each banner a **layout archetype** (`Crossed` = title/CTA left + giant hook right over a decorative background; `ImageLed` = left text rail over an image-led composition with the subject on the right), chosen per banner and applied per carousel slide.
- Render active banners as a **full-bleed, autoplaying carousel** at the top of the home page, replacing the current static store-name/value-banner hero block. The carousel orders by `SortOrder`, shows indicators and prev/next only when more than one banner exists, and never renders when no banner is active.
- Compose the overlay text in **editable sections** (title, hook, body, CTA), rendered as visually-styled spans (no heading tags) that reflow between desktop and mobile.
- Add a single admin **text-color choice** (`BrandLight` / `BrandDark` / `White` / `Black`) that deterministically sets both text color and a contrast scrim, so text/scrim can never be mismatched and WCAG AA is preserved over arbitrary admin images.
- Define the banner **link** as an internal target picker only (`Store` / `Category` / `Collection` / `Product`); the destination route is resolved server-side from the entity, and no raw URL is ever stored.
- **One required image** (the landscape cover) generates the density set; the mobile asset is **optional** and only used when the admin supplies it. When absent, the landscape cover is cropped/zoomed in CSS to fill the mobile box.
- Add the banner image pipeline (Skia resize → MinIO S3, no upscale) reusing the keyword cover service shape, and an admin CRUD surface (list + upsert with two optional image inputs, layout selector, color selector, link picker, text fields, `SortOrder`, `IsActive`).

## Capabilities

### New Capabilities
- `storefront/home-banners`: admin-managed home-page promotional banners — the `Banner` / `BannerImage` data model, layout archetypes, text-color contrast contract, internal link targets, the Skia/MinIO image pipeline (landscape density set + optional mobile asset), the admin CRUD surface, and the full-bleed autoplaying home carousel with its responsive desktop/mobile reflow and accessibility behavior.

### Modified Capabilities
- _(none)_

## Impact

- **Models** (`VaultShop.Models`): new `Banner`, `BannerImage`, and `BannerLayout` / `BannerTextColor` / `BannerTargetKind` / `BannerImageKind` enums.
- **DataAccess** (`VaultShop.DataAccess`): `BannerRepository` + interface, `IUnitOfWork` / `UnitOfWork` registration, `ApplicationDbContext` `DbSet`s, one EF Core migration.
- **Web** (`VaultShop.Web`): `Areas/Admin/Controllers/BannerController.cs` + `Views/Banner/{Index,Upsert}.cshtml` + `wwwroot/js/banner.js`; `Services/BannerImages/BannerImageService.cs`; `Areas/Customer/Controllers/HomeController.Index` banner loading; `HomeIndexVM.Banners`; `Views/Shared/_HomeBanner.cshtml`; `.banner__*` styles in `wwwroot/css/site.css`; admin nav entry in `Views/Shared/_Layout.cshtml`; es-AR/en-US `.resx`.
- **Reuse**: `IImageStorageService` (MinIO), `SkiaImageProcessor`, `SlugHelper`-adjacent conventions, and the existing Bootstrap 5 carousel already used on the product detail page.
- **Tests**: banner image service tests, ordering/visibility tests, link-resolution tests, and carousel render tests.
- **Specs**: new `openspec/specs/storefront/home-banners/spec.md`.
