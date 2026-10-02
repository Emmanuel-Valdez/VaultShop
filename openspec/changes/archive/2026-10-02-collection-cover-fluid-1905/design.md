## Context

See proposal.md Why. Current state: `KeywordImageService` derives three different-ratio crops (`1600x700` / `1200x500` / `780x520`); `site.css` pins `--hero-h` at `560/320/260` with `object-fit:cover`; `_CollectionHero.cshtml` (plus the Customer-area copy) emits per-band `<source>` elements and renders title + slug + count; `keywordUpsert.js` checks uploads against a stale hardcoded `16/9`; `SkiaImageProcessor.WriteCroppedJpeg` draws with default sampling at `JpegQuality = 75`. The theme overrides `--bs-secondary(-rgb)` to surface colors, which poisons every `badge bg-secondary` / `badge text-bg-secondary` (light-on-light in light mode, white-on-light in dark mode).

## Goals / Non-Goals

**Goals:**
- One master ratio end to end (`1905x714`), zero crop at every width, no rails, no mobile variant.
- Overlay reduced to title + count (slug removed).
- Visible quality fix without changing product/category image behavior.
- Legacy covers keep rendering until re-uploaded.

**Non-Goals:**
- New routes, URL slugs, sitemap entries, or checkout/payment changes.
- Product-variant, coupon, or shipping-cost work.
- Redesigning the admin theme or Bootstrap variable scheme.

## Decisions

- **Variant set: same ratio, three widths (`1905` / `1280` / `768` at 2.67:1).** Keeps density coverage (retina phones ~768, laptops ~1280, wide desktop ~1905) while deleting ratio divergence. Alternative (single 1905 file + browser downscale) rejected: wastes bytes on phones.
- **Pure fluid, no rails, no mobile variant.** `height = 100vw / 2.67` at every width (`~120px` at `320px`, `~719px` at `1920px`). Rails rejected: any pinned height forces cropping, ruled out as unpredictable for the banner designer. A dedicated mobile variant was considered and rejected by user vote: one master, one preview, one mental model. Accepted: a short strip on rare very narrow phones; a tall hero at `1920px` pushing the catalog below the fold.
- **Overlay without slug.** Drop the `Slug` span from both `_CollectionHero.cshtml` copies; title + count remain. Verify `CollectionHeroVM.Slug` usages first — prune the property only if unused elsewhere.
- **`srcset`/`sizes` on one `<img>`, no `<picture>`.** Ratio never varies, so band switching has nothing to select; density selection is exactly what `srcset` is for. Deletes the `<source>` machinery in both `_CollectionHero.cshtml` copies.
- **Legacy fallback: widest available crop as plain `<img>`.** No migration of stored objects; re-upload regenerates. `CoverMedium`/`CoverSmall` kinds stay queryable until the last legacy row is replaced, then the kinds can be retired by a later change.
- **Quality floor in `SkiaImageProcessor`:** high-quality sampling paint on the crop draw, JPEG quality ~82-85, explicit non-premultiplied sRGB bitmap before encode, and a minimum master width guard so the pipeline refuses to upscale (validation error naming the minimum). Product (`contain`) and category (square chip) callers share these paths — their dimensions/ratios stay untouched; only sampling/quality/alpha handling changes, which strictly improves them too.
- **Ratio check `1905/714 ±5%` in `keywordUpsert.js`**, fed by the existing `data-crop-hint` attribute. Replaces the stale `16/9` constant; resx hint/help strings updated to `1905x714` / `2.67:1` in both cultures.
- **Badge contrast: one rule scoped to `.badge` in `site.css`** (solid secondary gray bg + white text in both themes), instead of editing five views. Fixes Keyword x2, Category, Product count, Version, and `keyword.js` placeholder at once; the `--bs-secondary` surface override stays for navbar/offcanvas which need it. Scoping to `.badge` (not `.bg-secondary` globally) avoids changing navbar/offcanvas surfaces.

## Risks / Trade-offs

- [Risk] `1905px` master heavier than old `1600px` → mitigated by `srcset` density selection; phones fetch the 768 variant.
- [Risk] `~719px` hero at `1920px` pushes the catalog below the fold → accepted and specified; the alternative (rails) costs cropping, which was ruled out.
- [Risk] `~120px` mobile strip leaves ~40px of pure image under the title + count overlay → mitigated by the scrim plus a visual check with a long title over a busy photo during implementation (task 2.1).
- [Risk] Shared `SkiaImageProcessor` changes affect product/category encodes → mitigated by keeping their target sizes/ratios identical; sampling/quality changes are improvements, verified by existing `ProductImageServiceTests`.
- [Risk] Legacy 3-row keywords render old ratios until re-upload → accepted; fallback path specified and the admin reference text tells the admin to re-upload.

## Migration Plan

1. Deploy code; legacy covers render via fallback, no data migration.
2. Re-upload collection covers over time (admin reference text guides this).
3. Later change (out of scope): retire `CoverMedium`/`CoverSmall` kinds once no rows remain.

## Open Questions

None — single ratio with no rails, title + count without slug signed off (`120px` at `320px` accepted as inevitable; very narrow phones are rare).
