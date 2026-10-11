## 1. Data model and persistence

- [x] 1.1 Add `SafeZoneLeft`, `SafeZoneTop`, `SafeZoneWidth`, `SafeZoneHeight` (nullable `decimal?`, `[Range(0,1)]`) to `Banner`; verify the build compiles
- [x] 1.2 Add EF migration mapping the four fields as `decimal(4,3)` and update `ApplicationDbContextModelSnapshot`; verify `dotnet build` and migration applies cleanly to a fresh database
- [x] 1.3 In `BannerController.Upsert` (POST) enforce all-or-nothing (clear partial sets), and bounds check `left + width <= 1` and `top + height <= 1` with a localized ModelState error before persistence; verify it returns the form with an error and does not persist on invalid input
- [x] 1.4 Ensure the edit path preserves existing image rows and does not clobber safe-zone values on partial-save vector; verify with the existing upsert tracking regression test staying green

## 2. Renderer safe-zone inset

- [x] 2.1 In `_HomeBanner.cshtml` compute insets from the normalized rect when all four values are present and emit `--banner-safe-top/right/bottom/left` CSS variables on the `carousel-item`; verify a banner with a rect renders the style attributes
- [x] 2.2 Change `.banner__content` in `site.css` from `inset: 0` to `top/right/bottom/left: var(--banner-safe-*, 0)` so layouts fall back to the current geometry with no safe zone; verify the home-banners render tests still pass unchanged
- [x] 2.3 Add `BannerRenderTests` cases: safe zone present emits expected percentages; safe zone absent emits no `--banner-safe-*` variables; verify the new tests pass and the existing suites stay green

## 3. Live preview and safe-zone editor

- [x] 3.1 Replace the edit-only "Banner Images" panel in `Upsert.cshtml` with a live preview: desktop box (`aspect-ratio: 1905/714`) and mobile box (portrait `4/5`), images from stored variants or `URL.createObjectURL()` of a just-chosen `coverFile`/`mobileFile`, with the DeleteImage forms preserved for persisted banners; verify manually via browser testing on both new and existing banners (mobile geometry and preview placement refined in Section 5)
- [x] 3.2 Add a desktop/mobile toggle and wire form `input`/`change` events to re-render the preview from live form state (title, hook, body, CTA, layout, text color); verify preview updates without a page reload
- [x] 3.3 Create `wwwroot/js/bannerPreview.js` implementing the safe-zone rectangle on the desktop preview (pointer draw/resize), writing normalized 3-decimal values to four hidden inputs `Banner.SafeZoneLeft/Top/Width/Height`, clamping and rounding within `[0,1]`, honoring an existing rect on load, and mirroring the rectangle at the same percentages on the mobile box; verify via browser testing (draw, resize, reload restores it)
- [x] 3.4 Add a "No safe zone" reset action that clears the four hidden inputs and hides the rectangle; verify all-or-nothing postback (empty rect clears prior values)
- [x] 3.5 Add localized resx keys (en + es) for the preview panel, desktop/mobile toggle, safe-zone hint, reset action, and the bounds/all-or-nothing validation message; verify keys resolve in both cultures

## 4. Integration verification

- [x] 4.1 Add `BannerAdminHttpTests` coverage: valid rect round-trips through POST and reload; partial or out-of-bounds rect fails validation and does not persist; empty rect clears a prior value; verify the suite passes
- [x] 4.2 Run the full test suite (`dotnet test VaultShop.sln`) and verify all tests pass, including the archived home-banners suite with no rendering regressions
- [x] 4.3 Manual browser check of the Upsert page in es-AR: preview renders, safe-zone draw/reset works, and a saved safe zone confines the rendered overlay at desktop and mobile widths on the home page

## 5. Review refinements (folded into this change)

- [x] 5.1 Unify mobile geometry: in `site.css` the `@media (max-width: 991.98px)` block uses `aspect-ratio: 4 / 5` for both `.carousel-item.banner--layout-crossed` and `.banner--layout-imageled` (drop `213 / 100` and `min-height`); update `.banner-preview__mobile--crossed` to `4 / 5`; verify the Crossed cover is center-cropped (not distorted) and both layouts have the same mobile height
- [x] 5.2 Make the admin preview mirror the storefront art-direction: `bannerPreview.js` selects the mobile image with `imageLed ? (mobileObjUrl || storedMobile || cover) : cover`, and the server-rendered mobile frame `src` in `Upsert.cshtml` follows the same rule; verify Crossed shows the cover and ImageLed shows the mobile asset, matching the home
- [x] 5.3 Move the desktop preview to a full-width section at the top of `Upsert.cshtml` with `.u-breakout` and `aspect-ratio: 1905 / 714` (move `data-banner-preview` to the view root); keep the mobile frame and the DeleteImage forms in the sidebar; remove the desktop/mobile toggle and use `PreviewDesktop`/`PreviewMobile` as section headings; verify the desktop preview type renders at live size
- [x] 5.4 Fix safe-zone rounding in `bannerPreview.js` `writeRect` (`w = min(round3(w), round3(1-l))`, `h = min(round3(h), round3(1-t))`); verify a draw that previously wrote `l + w = 1.001` now stays within bounds and saves without the `SafeZoneInvalid` error
- [x] 5.5 Add a localized hint (en + es) that the mobile image is used only by the "Image protagonista" layout; verify it resolves in both cultures
- [x] 5.6 Add a regression test that changing a banner's layout to `ImageLed` persists and the home emits the mobile `<source>`; re-run `dotnet test VaultShop.sln` and the browser checks (desktop full-width preview; mobile at ~400px for both layouts)