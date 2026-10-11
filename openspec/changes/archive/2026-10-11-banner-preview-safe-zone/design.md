## Design

### Data model

`Banner` gains four nullable `decimal` properties — `SafeZoneLeft`, `SafeZoneTop`, `SafeZoneWidth`, `SafeZoneHeight` — each `[Range(0, 1)]`, stored all-or-nothing. EF maps them as `decimal(4,3)` (covers `0.000`–`1.000`). All-or-nothing is enforced in the controller: if any of the four is null, the rest are cleared to null; otherwise data annotations validate range and the controller checks `left + width <= 1` and `top + height <= 1` with a localized error. No new entity, no new table — columns on `Banners` only.

### Renderer (inset)

`VaultShop.Web/Views/Shared/_HomeBanner.cshtml` computes the insets from the normalized rect when all four values are present:

- `top = SafeZoneTop*100%`, `left = SafeZoneLeft*100%`
- `right = (1 - (SafeZoneLeft + SafeZoneWidth))*100%`
- `bottom = (1 - (SafeZoneTop + SafeZoneHeight))*100%`

and emits them on the `carousel-item` as CSS variables: `style="--banner-safe-top:..%; --banner-safe-right:..%; --banner-safe-bottom:..%; --banner-safe-left:..%"`. In `site.css`, `.banner__content` changes from `inset: 0` to:

```css
.banner__content { position: absolute; top: var(--banner-safe-top, 0); right: var(--banner-safe-right, 0); bottom: var(--banner-safe-bottom, 0); left: var(--banner-safe-left, 0); }
```

No inline styles, no safe zone → CSS defaults keep the current `inset: 0` fixed-rail behavior exactly (the fallback). The inner layout rules (Crossed grid areas, ImageLed rail, mobile reflows) and the scrim stay untouched, so they only ever lay out *inside* the inset box. The same CSS vars ride the art-directed flow: below 992px the mobile composition (mobile asset or CSS-cropped cover) uses the identical proportional insets, satisfying the "proportional to mobile" requirement without any JS.

### Admin form and preview

`Upsert.cshtml`:

- Add four hidden inputs: `Banner.SafeZoneLeft`, `Banner.SafeZoneTop`, `Banner.SafeZoneWidth`, `Banner.SafeZoneHeight`, bound to the banner model.
- Replace the right-hand "Banner Images" panel (currently edit-only) with a live preview card that renders always:
  - Desktop composition as a fixed-ratio box (`aspect-ratio: 1905 / 714`), image from the stored cover (Master/Medium/Small, same fallback as the renderer) or a `URL.createObjectURL()` of a just-chosen `coverFile`.
  - Mobile composition box: unified `aspect-ratio: 4 / 5` for both layouts (see Refinements); image from the stored mobile asset or a chosen `mobileFile` when `ImageLed`, else the cover URL (mirrors the live site's art-direction).
  - A desktop/mobile toggle; the preview re-renders from the live form fields on `input`/`change`.
  - A DeleteImage form preserved inside the panel when the banner is persisted.
- The safe-zone editor overlays the desktop box: a semi-transparent rectangle, initialized from existing coordinates when present, created on pointer press and resized on drag. It writes normalized values (`x/width`, `y/height` as 3 decimals, clamped, rounded) back to the hidden inputs, and mirrors the rectangle over the mobile box at the same percentages.
- Keyboard/pointer selectable rectangle; `aria-label` on the editor; values via `<input type="range">`-free code — pointer events on the overlay, with the rectangle kept within box bounds.
- A "No safe zone" reset action clears all four fields and hides the rect (satisfies all-or-nothing for the no-zone case).

`wwwroot/js/bannerPreview.js` (new file, loaded via `@section Scripts`): reads the current form state, paints the preview and the rectangle, and writes the hidden inputs. Reuses no framework; plain DOM + Pointer Events.

### Controller

`Upsert` (POST) adds validation before persistence:

- normalize all-or-nothing (clear partial sets),
- `[Range(0,1)]` handled by data annotations; bounds check `left+width <= 1`, `top+height <= 1` → localized ModelState error (same path as image validation, leaves the banner untouched),
- then proceeds with existing save flow. `BannerVM` unchanged; the edit path already preserves untouched fields (`obj.Images = existing.Images`).

### Localization

New resx keys (en + es) on `Views/Banner/Upsert.*`: preview panel title, desktop/mobile toggle, safe-zone hint, reset action, plus validation message on `BannerController.*` for bounds/all-or-nothing.

### Tests

- `BannerRenderTests`: banner with a safe zone → `--banner-safe-*` vars present with expected percentages; banner without → no inline vars (proves fixed-rail fallback). Duplicate the two existing assertion styles.
- `BannerAdminHttpTests`: POST upsert with a valid rect persists (`SafeZone*` survive reload); POST with a partial set or out-of-bounds rect lands back in the form with a validation message and does not persist; POST with all values empty clears any prior rect.
- Existing 25/25 home-banners tests must stay green (fallback guarantees no rendering regression).

### Assumptions

- One normalized rect applies to **both** cover and mobile proportionally (no per-image rects) — matches the single-rect editor and the "same rectangle mirrored" requirement.
- Safe zone confines only the overlay **content**; the full-bleed contrast scrim is unchanged (its behavior is spec'd separately and the content never leaves the rect).
- Preview re-renders client-side only; after a failed save postback the just-chosen file is gone (browser can't persist object URLs), so the preview falls back to stored images or an empty state until re-chosen.

### Refinements (post-review, folded into this change)

These supersede earlier details where they conflict.

**Unified mobile geometry.** Below 992px both layouts use `aspect-ratio: 4 / 5` on the storefront (`site.css`). `Crossed` drops `213 / 100` and `min-height`; background-type covers are center-cropped to 4:5, and `ImageLed` keeps its portrait art-directed composition. The admin mobile preview mirrors this (`.banner-preview__mobile--crossed` → `4 / 5`).

**Preview ↔ storefront art-direction parity.** The storefront uses the mobile asset only for `ImageLed` (`artDirected = ImageLed && mobile`). The preview must mirror this exactly: `mobileUrl = imageLed ? (mobileObjUrl || storedMobile || cover) : cover`. Previously the preview showed the stored mobile asset for every layout, contradicting the home and reading as a bug ("preview ok, home uses the desktop image"). The server-rendered mobile frame `src` follows the same rule. A form hint states the mobile image is used only by the ImageLed layout.

**Desktop preview at real scale.** The desktop preview moves out of the `col-lg-4` sidebar to a full-width section at the top of the Upsert view (`data-banner-preview` moves to the view root; the mobile frame and the DeleteImage forms stay in the sidebar). The desktop frame uses `aspect-ratio: 1905 / 714` with `.u-breakout` (100vw) so the `vw`-based `clamp()` type renders at the same size as the live banner (fixes the oversized-text-in-a-tiny-box problem). The desktop/mobile toggle is removed (both frames always visible); `PreviewDesktop` / `PreviewMobile` become section headings so the resx keys and the localization test stay valid.

**Safe-zone rounding.** `writeRect` rounds each coordinate to 3 decimals independently, so independently-rounded `l + w` (or `t + h`) can exceed `1` (e.g. `0.124 + 0.877 = 1.001`) and trip the controller bounds check — the reported "fuera de límites" error. Fix: after rounding, clamp `w = min(round3(w), round3(1 - l))` and `h = min(round3(h), round3(1 - t))`. Both stay exact multiples of 0.001 and can never exceed the bounds. No controller change.