## Why

Banner overlays place text with fixed rail/padding geometry tuned to an abstract hero image, so the subject of a real photo can end up directly under the text, hurting legibility and visual quality. Admins only see upload thumbnails in the form — they cannot see how a banner actually renders until it ships to the home page.

## What Changes

- `Banner` records gain an optional **safe zone**: four nullable normalized coordinates (`left`, `top`, `width`, `height`, each in `[0,1]`) relative to the cover image, stored all-or-nothing, and applied proportionally to both the landscape cover and the portrait mobile art.
- The storefront renderer (Sectioned overlay text / layout archetypes) confines the overlay content inside the safe zone when one is set; when none is set, banners fall back to the current fixed-rail geometry, so existing banners render byte-for-byte unchanged. The text-color/scrim contract is untouched.
- The Banner Upsert form replaces the edit-only "Banner Images" thumbnails panel with a **live preview**: it renders from the current form fields (text, layout, color) plus the stored or just-chosen image (via object URLs, no server round trip), with a desktop/mobile toggle, and a pointer-drawn safe-zone editor that writes the normalized rectangle back to the form. Works for new banners too.
- Localized admin strings (en-EN/es-AR) for the new preview panel and validation messages.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `storefront/home-banners`: gains an optional safe-zone constraint on overlay rendering (ADDED requirement) and a live admin preview with a safe-zone editor (ADDED requirement). Existing requirements keep their current behavior when no safe zone is set.

## Impact

- `VaultShop.Models/Banner.cs`: +4 nullable `decimal` safe-zone fields.
- EF Core migration + `ApplicationDbContextModelSnapshot` for the `Banners` table.
- `VaultShop.Web/Views/Shared/_HomeBanner.cshtml` + `wwwroot/css/site.css`: emit safe-zone inset via `--banner-safe-*` CSS variables when set; fixed-rail geometry remains the default.
- `VaultShop.Web/Areas/Admin/Views/Banner/Upsert.cshtml`: live preview panel replacing the images panel, safe-zone editor, hidden form fields.
- `VaultShop.Web/Areas/Admin/Controllers/BannerController.cs`: accept and validate the four safe-zone fields.
- `VaultShop.Web/wwwroot/js/bannerPreview.js`: preview render + pointer drawing of the rectangle.
- Resx: `BannerController.*` and `Views/Banner/Upsert.*` (en + es).
- Tests: `BannerRenderTests` (inset emission + fallback), `BannerAdminHttpTests` (round-trip, all-or-nothing validation).