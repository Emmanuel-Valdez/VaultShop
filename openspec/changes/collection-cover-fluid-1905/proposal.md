## Why

Collection hero covers still crop aggressively at wide viewports (up to ~33% height loss at 1920px) and look degraded ("se ve rara"). One fixed crop ratio per band cannot match a hero box whose ratio varies continuously with viewport width, and the SkiaSharp pipeline minifies without high-quality sampling at JPEG q75. The user signed off on the fluid system A (aspect-ratio-locked, zero crop in-band) with master `1905x714`.

## What Changes

- Single cover master `1905x714` (R 2.67) replaces the three band crops (`1600x700` / `1200x500` / `780x520`); one stored variant set at that ratio (widths for density, e.g. 1905 / 1280 / 768) instead of three different ratios.
- Hero becomes fluid with no clamping: `height = 100vw / 2.67` — zero crop at every width (`~120px` at `320px`, `~719px` at `1920px`). No rails: any pinned height would force cropping, ruled out as unpredictable for the banner designer. Accepted: a short strip on very narrow phones (rare) and a tall hero on very wide desktops.
- `<picture>` band machinery collapses to a single-ratio `srcset` (same ratio, several pixel sizes); legacy 3-row keywords keep rendering via fallback until re-uploaded.
- Hero overlay drops the slug: title + product count only.
- Image quality floor: high-quality sampling on downscale, JPEG quality 75 -> ~82-85, explicit sRGB non-premultiplied bitmap, minimum master resolution so the pipeline never upscales; advisory upload warning keyed to the new ratio (replaces the stale hardcoded `16/9` check in `keywordUpsert.js`).
- Admin image label badges (`Keyword/Upsert`, `Category/Upsert`, `Product/Upsert` count, `System/Version`) get a contrast fix: one scoped CSS rule so badge text stays readable in light and dark themes (today: light-on-light in light mode, white-on-light in dark mode, because `--bs-secondary` is overridden to a surface color).
- **BREAKING** (data, with fallback): keywords with legacy 3-ratio crops render through the fallback path; re-upload regenerates the single-ratio set. No URL or route changes.

## Capabilities

### New Capabilities

- None — behavior modifies existing capabilities.

### Modified Capabilities

- `catalog/collection-hero`: pure-fluid ratio-locked hero (single master ratio, no rails, zero crop at every width) replaces fixed heights (560/320/260) and per-band crop selection; overlay renders title + count without the slug; quality floor for hero encodes.
- `catalog/keywords`: cover upload produces a single-ratio variant set from a `1905x714` master; admin reference text and mismatch warning name the new master size/ratio; legacy 3-row coexistence and fallback defined.

## Impact

- `VaultShop.Web/Services/KeywordImages/KeywordImageService.cs` (crop constants + variant set), `VaultShop.Web/Services/ImageProcessing/SkiaImageProcessor.cs` (sampling, quality, alpha flatten — shared with product/category paths, behavior for those callers must not change), `VaultShop.Web/wwwroot/css/site.css` (fluid hero + badge rule), `Views/Shared/_CollectionHero.cshtml` (+ Customer copy), `wwwroot/js/keywordUpsert.js` (ratio check), Keyword/Category/Localization resx strings, `VaultShop.Tests/CoverCropGeometryTests.cs` + `SearchHttpTests.cs` asserts.
