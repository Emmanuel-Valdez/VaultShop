## Context

See `proposal.md` — Why for motivation. The constraints that shape the approach:

**The hero box ratio is not stable, and that is the whole problem.** `site.css` sets `--hero-h: 320px` by default and at `≤991.98px`, dropping to `260px` at `≤767.98px`. So the rendered box ratio (`viewportWidth / heroH`) ranges from `1.23:1` (320px phone) to `6.0:1` (1920px desktop) — a **4.9x spread**. A single fixed-ratio source is cropped against all of it, and because `object-fit:cover` picks the binding axis per viewport, the crop axis itself flips: sides on narrow screens, top/bottom on wide ones.

Current behavior with the stored `1400x500` (`2.8:1`), derived from `KeywordImageService.cs:11-12` and `site.css:1027-1035`:

| Viewport | heroH | Box ratio | Crop axis | Visible |
|---|---|---|---|---|
| 320 | 260 | 1.23:1 | sides | 44% |
| 390 | 260 | 1.50:1 | sides | 54% |
| 576 | 260 | 2.22:1 | sides | 79% |
| 768 | 320 | 2.40:1 | sides | 86% |
| 992 | 320 | 3.10:1 | top/bottom | 90% |
| 1280 | 320 | 4.00:1 | top/bottom | 70% |
| 1920 | 320 | 6.00:1 | top/bottom | **47%** |

**The existing non-square path letterboxes.** `SkiaImageProcessor.WriteResizedJpeg` (`:148-155`) computes `scale = Math.Min(...)` and centers the result on a `SKColors.White` canvas. That is why the stored size is a fixed `1400x500` regardless of upload: contain has to be told a canvas. Switching to crop removes the constraint entirely.

**`WriteResizedJpeg` is shared and cannot be changed in place.** `ProductImageService.cs:81` calls it with `squareCrop: false` for product photos at `1000x1200` and depends on the contain behavior. `CategoryImageService.cs:63` and the keyword chip use `squareCrop: true`.

**The data model needs no migration.** `KeywordImage.Kind` is a plain `int` column (`20260917011744_AddKeywordCollections.cs:37`) and `IX_KeywordImages_KeywordId_Kind` **is** unique (`unique: true` at `:86`), so appending enum members is schema-compatible — the three crop rows differ in `Kind`, keeping `(KeywordId, Kind)` unique with one row per kind.

**Storage already namespaces per keyword and cannot collide.** `MinioImageStorageService.SaveObjectAsync` builds `objectKey = "{prefix}/{Guid.NewGuid():N}.jpg"`, so multiple objects under `keywords/keyword-{id}/` are safe. Note `DeleteObjectRequest.ExpectedPrefix` is a **guard**, not a prefix delete — the delete targets one exact `ObjectKey`. Per-row deletion is therefore row-count-dependent, not prefix-dependent.

## Goals / Non-Goals

**Goals:**
- Cut worst-case visible-area loss from 46% (mobile sides) and 53% (desktop top/bottom) to bounded values, verified per band.
- One admin upload, one authoring reference, no admin judgment about ratios.
- Zero regression for existing covers, zero migration, zero change to product or category images.
- One implementation of the crop math.

**Non-Goals:**
- Exact (zero-crop) rendering at every viewport. The box ratio spread is 4.9x; exact would need a crop per viewport.
- Backfilling or re-encoding existing covers. Legacy covers keep rendering as they do today.
- Changing the hero's fixed heights, scrim, overlay, or parallax. This change makes the existing render correct, not different.
- Server-side `<picture>` generation or `Accept`-based negotiation. Static markup plus three `<source>` elements is sufficient.
- `srcset`/`sizes` density descriptors. Bands are ratio-driven, not density-driven.

## Decisions

### D1 — Three crops, not one and not two

The band structure is what forces the count. A single crop must serve the full `1.23:1`–`6.0:1` range. Two crops split at the existing `767.98px` breakpoint still leave each band spanning ~2.4x of ratio (`320–767px` at `heroH:260` is `1.23:1`–`2.95:1`; `768–1920px` at `heroH:320` is `2.4:1`–`6.0:1`), and the best achievable worst case for a two-way split is **~36% loss** — worse than today's 46%/53% at the widths people actually use. A three-way split is the smallest count that lands every common viewport near zero crop.

Bands are chosen so the two widest-ratio boundaries fall where a crop is *exactly* right:

| Band | `<source media>` | Viewports | heroH | Box ratio range | Crop | Crop ratio | Exact at |
|---|---|---|---|---|---|---|---|
| Small | `(max-width:479.98px)` | 320–479 | 260 | 1.23:1 – 1.84:1 | `780x520` | 1.5:1 | 390px |
| Medium | `(min-width:480px) and (max-width:991.98px)` | 480–991 | 260→320 | 1.85:1 – 3.10:1 | `1200x500` | 2.4:1 | 768px |
| Large | `<img>` fallback | 992+ | 320 | 3.10:1 – 6.0:1 | `1600x400` | 4.0:1 | 1280px |

Resulting loss, worst case per band: **23%** (sides, at 480px — 11.5% per side), **23%** (top/bottom, at 992px), **33%** (top/bottom, at 1920px — 16.5% per side). Everywhere else under 11%.

`991.98px` reuses the boundary already in `site.css:1053`. `479.98px` is new and pairs with the `heroH` change at `768px`, so the medium band is the only one straddling two heights.

**Alternatives considered:** a taller stored source (e.g. `1400x700`) with `object-position` tuning — rejected, it re-aims the crop window but recovers nothing already cropped. Lowering `heroH` on phones to buy ratio — rejected, that trades a design decision from the editorial change for source pixels, and the band still spans 2.4x.

### D2 — 4:1 for the large crop, not 6:1

A `6:1` crop would be exact at 1920px but would crop **30% per side** at 768px and 16.5% at 1280px. `4:1` inverts that: exact at 1280px (the most common desktop width), 11% top/bottom at 1440px, and 23% at the 992px edge. The residual failure is a **symmetric top-and-bottom bleed**, which reads as intentional full-bleed, whereas a horizontal crop tends to bisect the subject.

**Crop axis is a design constraint, not just math:** a side crop cuts through the middle of a subject, a top/bottom crop removes sky and ground. This asymmetry is why the medium band's worst case (sides, 480px) is tuned tighter than the large band's (top/bottom, 1920px).

### D3 — Master `1920x1080` (16:9), published in admin

The master must satisfy the binding dimension of all three crops: `1600` wide (large) and `520` tall (small). `1920x1080` clears both with margin and is a standard export size, so admins are not asked to produce an unusual dimension. Every crop is a center crop of it:

- `1600x400` ← center `1920x480` of the master, downscaled
- `1200x500` ← center `1920x800`, downscaled
- `780x520` ← center `1620x1080`, downscaled

`1920x1080` is a **recommendation, not a constraint** — the crops are center crops, so any master yields full-bleed output. The published reference exists because a correctly-proportioned master avoids depending on the center crop to rescue the composition, not because a wrong ratio is rejected.

### D4 — `Cover` (existing) becomes the large crop; no backfill

`CoverMedium = 2` and `CoverSmall = 3` are added; `Cover = 1` is redefined from "the only cover" to "the large crop". A keyword with only a legacy `Cover` row therefore emits **no `<source>` elements** and its single image serves every band — byte-for-byte today's behavior. New uploads write all three rows.

This avoids a fourth stored object (a `Cover` row duplicating the large crop), avoids a backfill job, and avoids a data migration on live rows. The alternative — redefining `Cover` to a *new* value and leaving legacy rows as an orphan kind — would need a backfill and would leave the enum ambiguous.

### D5 — Additive `WriteCroppedJpeg`; `WriteResizedJpeg` untouched

A new public `WriteCroppedJpeg(original, stream, targetWidth, targetHeight)` implements cover-crop. `WriteResizedJpeg`'s contain branch is **not modified**, because `ProductImageService` depends on it — changing it would silently alter product photo framing, which is out of scope and unreviewable here.

Both the new method and the existing `squareCrop: true` branch delegate to one private `DrawCenterCrop` helper, since a square target is just the general case. The contain branch stays a separate ~6 lines. Net: the crop math exists once, no existing behavior changes.

**`KeywordImageService.SaveChipAsync` is untouched** — `400x400` square crop is already correct for the circular chip and is not viewport-dependent.

### D6 — Variant return type, not a `List<StoredImage>`

`SaveCoverAsync` returns a small record carrying the three `StoredImage` results keyed by crop role. The alternative is `IReadOnlyList<StoredImage>` and having the controller re-derive which is which by array position — positional coupling that silently corrupts if the order ever changes. A named record makes the controller's mapping explicit.

### D7 — Replacement must loop rows, and ordering is save-then-delete

`ReplaceImageAsync` currently fetches exactly one row (`KeywordController.cs:219`) and deletes one object. It must fetch **all cover rows** for the keyword (`GetAll(i => i.KeywordId == keywordId && i.Kind != KeywordImageKind.Chip)`), and delete each superseded `ObjectKey` individually, since `DeleteObjectAsync` targets one exact key.

The existing safety property is preserved and extended: save all three new objects **first**, commit the new rows, then best-effort delete the old objects. A failure mid-way leaves orphans in storage, never a keyword with a broken cover — the same trade-off the current code already makes, now across three objects instead of one.

The keyword **delete** path (`:188-203`) already loops all image rows and needs no change.

### D8 — ViewModel propagation, not a `ViewData` convention

`CollectionHeroVM` gains a `MobileImageUrl` (and a medium equivalent), `CollectionChipVM` gains the matching fields, and `HomeIndexVM.ComputeCollections` populates them. `HomeController` then sets the corresponding `ViewData` keys next to the existing `ActiveCollectionCover`.

The alternative — reading the keyword's images again inside the view — would issue a query per render. `ComputeCollections` already resolves chip and cover URLs for the whole collection list in one pass, so the new fields ride along for free and cost one extra `FirstOrDefault` over an already-materialized collection, not an extra query.

`HomeController.cs:106-113` builds a **second, inline** `CollectionChipVM` projection for the product-detail page. It is updated in the same change so the two projections do not diverge, even though the detail page does not render the hero.

### D9 — Advisory ratio warning, no server-side rejection

`keywordUpsert.js` already binds a `change` listener that builds a preview from `URL.createObjectURL` (`:11-20`). Reading `naturalWidth`/`naturalHeight` from that same `Image` after load adds the ratio check with no new file-wiring. The message is styled as advisory `form-text`, not an invalid-feedback class, and the server accepts any decodable image.

Rationale: the crops are center crops, so an off-ratio master produces a valid result. Rejecting uploads would be a false constraint and would break admins mid-task for something that still renders. The warning exists to improve composition, not to gate.

## Risks / Trade-offs

- **Cover storage grows ~2.4x** (three JPEGs instead of one; `1600x400` and `1200x500` are small files, `780x520` is the only meaningful addition). → Accepted; no retention policy exists to violate, and covers are a handful of rows. Revisit if collection count grows past the low hundreds.
- **Existing covers keep the old aggressive crop until re-upload.** → The spec requires an admin-visible notice on edit so this is discoverable, not silent. A bulk re-upload tool is deliberately out of scope.
- **A cover designed as a banner with baked-in text loses its edges**, where contain previously showed the whole image (with white bars). → Inherent to fixing the problem: the viewport was already discarding those edges. Called out in the proposal as a breaking visual change.
- **Two `<source>` elements plus a breakpoint set that must stay in sync with `site.css`.** → The band boundaries are derived from the `heroH` breakpoints, so a future `heroH` change can silently invalidate the crop ratios. Mitigation: a comment at the constants in `KeywordImageService` naming the `--hero-h` values they assume, plus the geometry test in the task list that fails if a crop ratio stops matching its band's box ratio.
- **Client-side ratio warning is advisory only**, so a mismatched master can still reach the server. → Accepted by D9; the server-side result is always valid.
- **The `479.98px` boundary has no counterpart in existing CSS.** → Intentional: it is a crop-ratio boundary, not a layout boundary, and is documented as such in `design.md` and the spec.

## Migration Plan

No schema migration, no data backfill, no deploy ordering constraint. Rollout is additive: the new enum members are unused until new uploads write them, and every render path falls back to `Cover` when a crop is absent.

**Rollback:** revert the deploy. New uploads written during the rolled-forward window leave orphan `CoverMedium`/`CoverSmall` rows and objects that nothing reads; the pre-change render path ignores them, and the keyword delete path already loops all image rows, so a later delete still cleans them up. No data repair is required to revert.

## Open Questions

None that change the specs, the approach, or the task breakdown.
