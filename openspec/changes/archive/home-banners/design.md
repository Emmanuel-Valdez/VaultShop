## Context

The home page (`Areas/Customer/Views/Home/Index.cshtml`) leads with a hardcoded, localized store-name heading plus a `home-value-banner` and a `home-trust-strip`; product, category and collection rows are auto-generated from catalog data. There is no admin-editable promotional surface.

The codebase already provides every infrastructure piece this feature needs, so the design is mostly composition and reuse rather than new machinery:

- Object storage via `IImageStorageService` (MinIO S3), with `SkiaImageProcessor` for validation, decode-with-orientation and cropped/resized JPEG output.
- A multi-image-per-entity precedent in `KeywordImage` (child table keyed by a `Kind` enum) and its service `KeywordImageService.SaveCoverAsync` (single landscape ratio, three densities, no upscale, smallest-variant gated).
- A single-image-per-entity precedent in `Category` (`ImageUrl` + `ObjectKey` + `FileName` + `ContentType` + `SizeBytes` + `StorageProvider`) and its `CategoryController` upsert/replace/delete-image flow.
- A full-bleed hero render + `srcset` + scrim precedent in `Views/Shared/_CollectionHero.cshtml` and `.collection-hero--breakout` / `.collection-hero__scrim` in `wwwroot/css/site.css`.
- A Bootstrap 5 carousel precedent on the product detail page (`Details.cshtml`).
- Repository/UnitOfWork registration pattern (`IUnitOfWork` + `UnitOfWork`) and admin DataTable CRUD (`Category`, `Keyword`, `Promotion` controllers + `wwwroot/js/*.js`).

Constraints: ASP.NET Core 8 MVC; Bootstrap 5; es-AR/en-US localization via `.resx`; WCAG, input validation and color contrast are non-negotiable; the same codebase deploys to two stores (VaultShop + Ukiyo) sharing code but with separate databases, so banner content is per-store data by construction.

## Goals / Non-Goals

**Goals:**
- Admin-managed, ordered, activatable home banners rendered as a full-bleed autoplaying carousel.
- Two layout archetypes (`Crossed`, `ImageLed`) selected per banner and applied per slide.
- Guaranteed legibility of admin-chosen text over arbitrary admin images via a single color choice that also fixes the contrast scrim.
- Internal-only link targets with server-side route resolution and safe handling of deleted targets.
- One required image (landscape cover) with an optional, higher-quality mobile asset; graceful CSS-based fallback when the mobile asset is absent.
- Full reuse of the existing image pipeline and admin CRUD conventions.

**Non-Goals:**
- Date/time scheduling of banners (activation is a boolean only).
- Raw URL / external link targets.
- Admin-authored layout presets beyond the two fixed archetypes, drag-to-reorder, or a configurable autoplay interval.
- An admin-drawn safe-zone rectangle for art-directed images.
- Per-culture banner content (one language per banner, matching existing catalog content).
- Banner click analytics/tracking.

## Decisions

### D1 — Entity `Banner` + child `BannerImage` keyed by `Kind`
`Banner` holds text, layout, color, target, order and active flag. Images live in a child table `BannerImage` with `BannerImageKind { Master, Medium, Small, Mobile }`, mirroring `KeywordImage`.

- *Why:* the optional mobile asset is a different ratio for a different role; a flat column set would need a parallel `(Url, ObjectKey, …)` triple per size and mixed-ratio columns. The child table gives a clean storage-delete loop and matches an established pattern.
- *Alternative considered:* flat 3-size columns on `Banner` (simpler for one density set, but awkward once the art-directed `Mobile` asset and its own metadata are added).
- *Naming:* `Banner`, not `PromotionalBanner`, to avoid collision with the existing `Promotion` discount model.

### D2 — Two layout archetypes via a `BannerLayout` enum
`BannerLayout { Crossed, ImageLed }`, rendered as a per-slide modifier class `.banner--layout-{a|b}` (layout is per banner, so a single carousel may mix archetypes).

- *Why:* the two compositions have genuinely different overlay arrangement and different crop contracts; an enum keeps the divergence to one column plus two CSS rule sets. The earlier "no presets" stance was invalidated once the owner confirmed both archetypes for v1.
- *`Crossed`:* CSS Grid with `grid-template-areas` — title/CTA left, hook spanning rows on the right on desktop; centered stacked order on mobile.
- *`ImageLed`:* flex column — left text rail over a full-bleed image whose subject sits on the right; on mobile the rail pins the title top-left and the CTA bottom-left via `margin-top:auto` (absence-tolerant, no fragile row indexing).

```
CROSSED  desktop                         CROSSED  mobile
┌───────────────────────────────┐        ┌───────────────────────┐
│ GABARDINA ACRÍLICA   5% OFF   │        │   GABARDINA ACRÍLICA  │ top-center
│ [ VER TODO ]         TRANSF.  │   →    │  5% OFF CON           │ center
│                      BANCARIA │        │  TRANSFERENCIA        │
└───────────────────────────────┘        │ [     VER TODO     ]  │ bottom, full width
                                         └───────────────────────┘

IMAGELED desktop (40/60)                 IMAGELED mobile
┌───────────────────────────────┐        ┌───────────────────────┐
│ [text rail]      [subject]    │        │ GABARDINA             │ title top-left
│  GABARDINA          ┌──┐┌──┐  │   →    │  ACRÍLICA             │
│  5% OFF             │m ││m │  │        │ 5% OFF CON            │
│  [ VER TODO ]       └──┘└──┘  │        │ [ VER TODO ] ┌──┐┌──┐ │ CTA bottom-left
└───────────────────────────────┘        └─────────────┴──┘┴──┘─┘
```

- *Breakpoint:* Bootstrap `lg` (992px), aligned with the `<picture>` media query so the layout inversion and the image swap happen on the same tick.
- *For `ImageLed` desktop the image is full-bleed behind an overlaid 40% text rail* (not a separate right column): the "subject stays visible because text is pushed left" behavior only makes sense if text overlaps the image plane with a reserved clear band, and it keeps desktop and mobile the same mental model (one cover layer, one scrim).

### D3 — Single text-color choice auto-driving text + scrim
`BannerTextColor { BrandLight, BrandDark, White, Black }` sets both `--banner-text` and `--banner-scrim` via modifier classes, so text and scrim can never be mismatched. The scrim is the WCAG guarantee; `text-shadow` is polish only (not counted toward contrast).

- Light text → dark scrim (alpha ≥ ~0.55); dark text → light scrim (alpha ≥ ~0.65, since brand-dark navy is not pure black). Targets: 4.5:1 body, 3:1 large.
- Scrim geometry is per archetype: a near-uniform vertical gradient for `Crossed` (text spans the box), and a left-anchored horizontal gradient for `ImageLed` that fades out before the subject region.
- Tokens: `BrandLight → var(--theme-surface)`, `BrandDark → var(--theme-primary)`, plus literal `#fff` / `#000`.
- *Risk note:* brand tokens are per-store configurable (`ThemeOptions`), so a store could set a light `--theme-primary`; the scrim is chosen against the light polarity whenever a token value is used, validated with the shipped defaults.

### D4 — Sectioned text as spans, no heading tags
Title, hook, body and CTA render as `<span>` elements with visual hierarchy; empty sections are omitted. The home `<h1>` is already the store name and the carousel mounts above it, so emitting headings per slide would corrupt heading order and duplicate headings across hidden slides.

### D5 — Internal-only link targets
`BannerTargetKind { Store, Category, Collection, Product }` + nullable `TargetId`. The destination is resolved server-side to the canonical route (`productDetails`, filtered `Search` with `keywordId`/`slug` or `categoryId`/`cslug`, or home `Index`). No raw URL is stored or accepted, eliminating open-redirect and `javascript:` vectors. A missing or soft-deleted target renders the slide without an anchor.

### D6 — Image pipeline and art-direction
Reuse `IImageStorageService` + `SkiaImageProcessor` in a new `BannerImageService` shaped like `KeywordImageService`:

- Required landscape cover (2.67:1) → `Master` (1905×714), `Medium` (1280×480), `Small` (768×288), no upscale, smallest-variant gated (partial sets allowed).
- Optional `Mobile` asset (4:5, ~1200×1500), gated by `CanDownscaleWithoutUpscale`.
- `ImageLed` renders a `<picture>`: `<source media="(max-width:991px)" srcset=Mobile>` + the landscape `<img srcset>` for desktop. When no `Mobile` asset exists, the landscape cover is cropped/zoomed with CSS (`object-fit: cover` + `object-position`) to fill the mobile box.

- *Why landscape-required, mobile-optional (P1) instead of mobile-required:* a single image cannot be optimal for both a 2.67:1 desktop box and a portrait mobile box — whichever axis is canonical, the other crop retains roughly 30% of an axis. Desktop is the larger, more prominent display, and the landscape cover reuses the collections pipeline almost entirely. Making the mobile asset optional means the admin is never forced to produce two images.
- *Mobile box ratios:* `Crossed` uses `aspect-ratio: 213/100` (~2.13:1) with a `min-height` guard, cropping the landscape cover ~10% per side (safe for a decorative background). `ImageLed` uses `aspect-ratio: 4/5`; when a matching mobile asset is supplied the crop is a no-op, otherwise the CSS crop/zoom fallback applies.
- *Alternative considered (P2):* mobile-required, desktop-degraded by cropping a portrait to landscape — rejected because it moves the lossy crop onto the most prominent display and diverges from the existing pipeline.
- *Alternative considered (P3):* single landscape with a non-overlay mobile band for `ImageLed` — rejected as it drops the intended overlay composition.

### D7 — Home carousel rendering
`HomeController.Index` loads active banners ordered by `SortOrder` (tiebreak `Id`) into `HomeIndexVM.Banners`; `Views/Shared/_HomeBanner.cshtml` renders a Bootstrap carousel replacing the static hero block. Controls (indicators, prev/next) render only when more than one banner is active; with zero active banners the carousel renders nothing. The image on the first slide loads eagerly with high fetch priority; the rest lazy.

### D8 — Autoplay and accessibility
Autoplay only when more than one banner, `data-bs-interval="6000"`, `data-bs-pause="hover"`. A visible pause/play toggle is mandatory (WCAG 2.2.2). A small JS init suppresses autoplay entirely when `prefers-reduced-motion: reduce` (extending the existing reduced-motion block at `site.css`). Each slide is `role="group"` + `aria-roledescription="slide"` + localized `aria-label`, the whole slide is one focusable `<a>` when a target exists, the CTA is a non-interactive `<span class="btn">` inside that anchor (no nested interactive element), and the image `alt` is the banner alt text or title.

### D9 — Data access and admin surface
`BannerRepository` + interface registered in `IUnitOfWork`/`UnitOfWork`; `DbSet<Banner>` and `DbSet<BannerImage>` on `ApplicationDbContext`; one EF Core migration. `Areas/Admin/Controllers/BannerController.cs` mirrors `Category`/`Keyword` (Index DataTable + Upsert with optional image inputs, replace/delete-image, delete). Nav entry in `Views/Shared/_Layout.cshtml`; es-AR/en-US `.resx`. `SortOrder` is a numeric field (no drag-reorder).

## Risks / Trade-offs

- **Per-store brand tokens can break the contrast assumption** (`--theme-primary`/`--theme-surface` are configurable) → scrim polarity is chosen against the light side for token values; validate with shipped defaults and known store themes.
- **CSS-crop fallback for `ImageLed` without a mobile asset may cut the subject** → documented admin guidance (compose the subject within the safe right region, keep top-left/bottom-left clear); the optional mobile asset is the quality path.
- **Removing the static store-name hero changes the home landing content** → the store name remains available via the site header/branding; the carousel becomes the primary hero. Rollback is reverting the view.
- **Autoplay can be intrusive** → mandatory pause control, reduced-motion suppression, pause on hover/focus, single-banner no-autoplay.
- **Overlapping with catalog/collection imagery** → banners are editorial/CTA surfaces, distinct from auto-generated catalog rows; the two can coexist without deduplication.
- **Two stores share code** → banner data lives in each store's database; no cross-store leakage; no shared assets beyond code.

## Migration Plan

1. Add `Banner` + `BannerImage` entities, DbSets, repository registration, and one EF Core migration. No data backfill (new surface).
2. Add `BannerImageService` and the admin controller/views/nav/resources and JS.
3. Wire `HomeController.Index` → `HomeIndexVM.Banners` and add `_HomeBanner.cshtml` + `.banner__*` CSS; remove the static store-name hero block from `Index.cshtml`.
4. Deploy per store; migrations apply with the app's normal startup migration path.
5. Rollback: revert the home view to restore the prior hero and drop the two tables; banner records are additive and safe to leave.

## Open Questions

- Whether to later let the admin draw an explicit safe-zone rectangle for art-directed images (deferred; fixed rail + content guidance first).
- Whether the autoplay interval should become configurable per store rather than a fixed 6000 ms.
- Whether additional layout archetypes (beyond `Crossed` / `ImageLed`) are wanted later.
