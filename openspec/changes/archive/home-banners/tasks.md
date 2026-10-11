## 1. Data model and persistence

- [x] 1.1 Add enums `BannerLayout`, `BannerTextColor`, `BannerTargetKind`, `BannerImageKind` and entities `Banner` and `BannerImage` (mirroring `KeywordImage`) in `VaultShop.Models`; verify `dotnet build VaultShop.sln` succeeds.
- [x] 1.2 Add `DbSet<Banner>` and `DbSet<BannerImage>` to `ApplicationDbContext` with the `BannerId` foreign key configuration; verify the context compiles and the model snapshot includes both entities.
- [x] 1.3 Add `IBannerRepository` / `BannerRepository` and register them in `IUnitOfWork` / `UnitOfWork`; verify the app starts and the repository resolves via DI.
- [x] 1.4 Create the EF Core migration for the two tables and verify it applies cleanly to a fresh test database (`dotnet test` migration/integration test).

## 2. Image pipeline

- [x] 2.1 Add `IBannerImageService` / `BannerImageService` reusing `IImageStorageService` + `SkiaImageProcessor`, shaped like `KeywordImageService`: validate (type/size/decodable), generate `Master`/`Medium`/`Small` at 2.67:1 without upscaling, and store under `banners/banner-{id}`; verify unit tests cover validation failure, undersized rejection, and partial-set generation.
- [x] 2.2 Add the optional `Mobile` asset path (4:5, ~1200×1500) gated by `CanDownscaleWithoutUpscale`; verify a unit test confirms the mobile variant is generated only when the source can cover it and is not required.
- [x] 2.3 Implement the image replace/delete cleanup loop (delete old object keys only after the new save succeeds); verify a test asserts old objects are removed and none remain after banner deletion.

## 3. Admin CRUD surface

- [x] 3.1 Add `Areas/Admin/Controllers/BannerController.cs` with `Index`, `Upsert` (GET/POST), `GetAll`, `Delete`, `DeleteImage` mirroring `CategoryController`; verify controller tests for create, edit-without-image, validation failure, and delete.
- [x] 3.2 Build `Views/Banner/Upsert.cshtml` with the layout selector, text-color selector, internal link picker (`TargetKind` + `TargetId` populated server-side from Products/Categories/Keywords), title/hook/body/CTA fields, `AltText`, `SortOrder`, `IsActive`, and the two optional image inputs; verify the form renders and round-trips a valid post.
- [x] 3.3 Add `Views/Banner/Index.cshtml` + `wwwroot/js/banner.js` (DataTable list) mirroring the existing admin tables; verify the list loads and Delete works end to end.
- [x] 3.4 Implement the server-side link resolver (`TargetKind` + `TargetId` → canonical route) with deleted/missing target rendering unlinked; verify unit tests for each target kind and the deleted-target case.

## 4. Storefront rendering

- [x] 4.1 Load active banners ordered by `SortOrder` (tiebreak `Id`) into `HomeIndexVM.Banners` in `HomeController.Index`; verify a test asserts only active banners are returned and in order.
- [x] 4.2 Add `Views/Shared/_HomeBanner.cshtml` rendering the carousel, slides (`role`/aria), the `<picture>` art-direction branch for `ImageLed`, sectioned text as spans with empty sections omitted, the scrim element, and the pause control; verify a render test asserts no heading tags are emitted and the CTA is a non-interactive element.
- [x] 4.3 Add `.banner__*` styles to `wwwroot/css/site.css`: `Crossed` grid-template-areas and `ImageLed` flex rail, the 992px breakpoint, mobile box ratios (`Crossed` 213/100 with min-height, `ImageLed` 4/5), text-color + scrim modifier classes with the left-anchored `ImageLed` gradient, and an inset `:focus-visible` ring; verify both layouts visually at desktop and mobile widths.
- [x] 4.4 Add the banner carousel JS init (interval 6000 ms, `prefers-reduced-motion` suppression, pause/play toggle) and extend the reduced-motion CSS block; verify autoplay runs with 2+ banners, is suppressed under reduced motion, and the pause control stops/resumes.
- [x] 4.5 Remove the static store-name/value-banner hero block from `Areas/Customer/Views/Home/Index.cshtml`; verify the home page renders the carousel on top and degrades to the prior layout when no banner is active.

## 5. Localization and navigation

- [x] 5.1 Add the admin nav entry for `Banner` in `Views/Shared/_Layout.cshtml` and es-AR/en-US `.resx` entries for the admin surface and the storefront carousel labels (slide N of M, previous, next, pause, play); verify labels switch with the language cookie.

## 6. Tests and verification

- [x] 6.1 Add tests covering every spec scenario not yet exercised: controls rendered only when more than one banner, no carousel when none active, each text-color choice maps to the expected text and scrim polarity, and the mobile-asset-present vs absent render branches.
- [x] 6.2 Run `dotnet build VaultShop.sln` and `dotnet test VaultShop.sln` and confirm all tests pass.
- [x] 6.3 Run a browser check on the home page (Playwright): autoplay and controls with 2+ banners, reduced-motion suppression, resolved internal link, `<picture>` mobile swap with the mobile asset present, and the CSS-crop fallback with it absent, for both `Crossed` and `ImageLed`.

## 7. Review fixes (pre-archive)

- [x] 7.1 Fix the EF tracking conflict on banner edit + image replace: the replace flow must query tracked rows (identity resolution) so `Remove` does not throw "another instance with the same key is already being tracked"; verify an HTTP regression test posts an edit with cover + mobile files and ends with replaced rows.
- [x] 7.2 Fix the blank home slide when the density set has no `Master` row: the render falls back to `Medium`/`Small` like the admin thumbnail; verify a render test with a partial set asserts the fallback `src`.
- [x] 7.3 Relax the optional mobile-asset gate from full 1200×1500 no-upscale to a 600×750 minimum (≤2× upscale into 1200×1500); update the validation messages and verify a phone-photo (1080×1350) is accepted while a sub-minimum source still yields null/error.
- [x] 7.4 Localize the banner admin surface (layout/text-color/target-kind dropdowns and table columns) and the promotion/coupon table enum columns through the controllers' resx; fix the `banner.js` `Yes`/`No` casing; verify HTTP tests render the es-AR labels in the form and in the DataTable JSON.
- [x] 7.5 Constrain the banner target picker to the chosen target kind (optgroup filtering by `TargetKind`, cleared on change, disabled for `Store`); verify the form HTML includes the filter script and the behavior in the browser or `BannerAdminHttpTests`.
