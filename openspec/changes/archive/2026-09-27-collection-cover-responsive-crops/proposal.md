## Why

Collection covers are stored once at a fixed `1400x500` and rendered with `object-fit:cover center` into a hero whose box ratio changes by more than 4x across viewports. Because the source ratio (`2.8:1`) is fixed, the browser crops on **different axes at different widths**, and the loss is severe at both ends:

- **Mobile** (`--hero-h:260px`): height is the binding constraint, so the sides are cut. At a 390px viewport only **54%** of the image width is visible — **46% is discarded** (23% per side). At 320px only 44% survives.
- **Desktop** (`--hero-h:320px`): at a 1920px viewport the box is `6:1` against a `2.8:1` source, so the top and bottom are cut. Only **47%** of the image height is visible — **53% is discarded**.

There is no single upload dimension that fixes this, because the hero box ratio spans `1.23:1` (320px phone) to `6:1` (1920px desktop) and a single fixed-ratio source must be cropped against all of it. Admins also have no in-app reference for what to upload, so every cover is uploaded blind.

## What Changes

- **BREAKING (visual, not API):** a keyword cover upload now produces **three stored crops** from one master image, and the hero picks the crop matching the viewport band via `<picture>`/`<source>`. Worst-case visible area improves from 46% lost (mobile sides) and 53% lost (desktop top/bottom) to **23% side crop on a narrow 480–575px window and 33% top/bottom on ≥1900px displays; ≤20% everywhere else.**
- **BREAKING (authoring):** the recommended master upload becomes **`1920x1080` (16:9)** instead of `1400x500`. The old `1400x500` is still accepted — it is center-cropped, not letterboxed, so no white bars appear.
- **BREAKING (processing):** the non-square branch of `SkiaImageProcessor.WriteResizedJpeg` currently *contains* the image onto a white canvas. Cover generation moves to true **center-crop** semantics via a new `WriteCroppedJpeg`. The existing method is left untouched because `ProductImageService` depends on its contain behavior.
- New `KeywordImageKind` values `CoverSmall = 2` and `CoverMedium = 3`; the existing `Cover = 1` is redefined as the large crop. **No EF migration** — `Kind` is an `integer` column and `IX_KeywordImages_KeywordId_Kind` is non-unique.
- The hero gains a mobile/medium/large crop chain and degrades gracefully: a keyword with only a legacy `Cover` row renders exactly as it does today, via the `<img>` fallback.
- The admin keyword form publishes the required master dimensions and the three generated crop sizes, and flags covers that still need a re-upload to gain the responsive crops.
- Corrects pre-existing drift in `catalog/collection-hero`: the spec still documents a progressive scroll collapse at `~400px`/`~260px`, but the implementation has used a **fixed** height (`320px`/`260px`) with a compositor-only parallax since the editorial change. That drift is repaired in the same spec because this change edits it and would otherwise be built on a false premise.

## Capabilities

### New Capabilities
<!-- None. This change extends existing catalog/collection-hero and catalog/keywords behavior. -->

### Modified Capabilities
- `catalog/collection-hero`: the hero source becomes viewport-band responsive; the documented heights and scroll behavior are corrected to match the implementation.
- `catalog/keywords`: keyword cover upload produces three derived crops from one master at a documented `1920x1080` size, with independent deletability per variant and a visible authoring reference in admin.

## Impact

**Models** — `VaultShop.Models/KeywordImage.cs` (2 new enum members), `CollectionChipVM.cs` and `CollectionHeroVM.cs` (+1 field each), `HomeIndexVM.cs` (populate the new field).

**Image processing** — `SkiaImageProcessor.cs` gains `WriteCroppedJpeg`; both it and the existing `squareCrop` branch delegate to one private crop helper so the crop math exists once. `WriteResizedJpeg`'s contain branch is **not** modified.

**Services** — `KeywordImageService.SaveCoverAsync` changes signature to return all cover variants; `IKeywordImageService` follows. `SaveChipAsync` (`400x400` square crop) is unchanged.

**Admin** — `KeywordController.ReplaceImageAsync` must remove and re-create every cover row (currently exactly one) and delete each superseded object key. `Admin/Views/Keyword/Upsert.cshtml` gains the dimension reference and a legacy-cover notice; `Upsert.es.resx` / `Upsert.en.resx` carry the localized strings.

**Storefront** — `Home/Views/Home/Search.cshtml`, `HomeController` (`ViewData` for the extra variant), and the shared `_CollectionHero.cshtml` partial render `<picture>`.

**Storage** — no schema migration and no backfill. Three objects per cover instead of one (all under the existing `keywords/keyword-{id}/` prefix, which already receives a GUID filename, so keys cannot collide). The keyword-delete path already loops all image rows and needs no change; **total cover storage grows roughly 2.4x**.

**Tests** — `KeywordImageServiceTests` extended for multi-variant output and crop geometry; existing product-image and category-image behavior asserted unchanged.
