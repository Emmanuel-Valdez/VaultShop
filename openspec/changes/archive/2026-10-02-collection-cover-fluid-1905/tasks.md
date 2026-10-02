## 1. Image pipeline (single ratio + quality floor)

- [x] 1.1 Retarget `KeywordImageService` to same-ratio variants (`1905`/`1280`/`768` at 2.67:1) with skip-crop-when-proportional and minimum-master guard, verified by new unit tests for variant sizes, zero-crop path, and upscale refusal
- [x] 1.2 Harden `SkiaImageProcessor` (sampling paint, JPEG quality ~82-85, non-premultiplied sRGB bitmap) without changing product/category target sizes, verified by existing `ProductImageServiceTests` plus a hero downscale test asserting no aliasing regression
- [x] 1.3 Update `keywordUpsert.js` ratio check to `1905/714 ±5%` and refresh cover resx strings (`es-AR` + `en-US`) to `1905x714` / `2.67:1`, verified by uploading a correct-ratio and a square file and observing warning only on the latter

## 2. Storefront hero (fluid + legacy fallback)

- [x] 2.1 Convert `site.css` hero to pure `aspect-ratio: 1905/714` (no clamp, no fixed heights) and both `_CollectionHero.cshtml` copies to single-ratio `srcset` with widest-legacy fallback while removing the slug span (title + count remain), verified at 320/768/1280/1920px showing zero crop at every width, a legible overlay at 320px (~120px tall, long title, busy photo), and the tall hero accepted at 1920px (~719px)
- [x] 2.2 Rewrite `CoverCropGeometryTests.cs` asserts for the single ratio with no rails and update `SearchHttpTests.cs` hero markup asserts (no slug), verified by `dotnet test` green

## 3. Admin badge contrast

- [x] 3.1 Add the scoped `.badge` contrast rule to `site.css` (both themes), verified visually in Keyword/Category/Product upserts and `System/Version` in light AND dark mode — no view edits needed

## 4. Verification

- [x] 4.1 Run `dotnet build VaultShop.sln` and full `dotnet test VaultShop.sln` green, and confirm parallax/reduced-motion behavior unchanged on a collection page
