## Context

See proposal — Why. Current state after `keywords-collections`:

- `Keyword.Slug` partial unique index, `SlugHelper.Slugify`, `CollectionChipVM.Slug` populated but never routed; cover `KeywordImage Kind.Cover` stored `1400×500` via `keywords/keyword-{id}` but never rendered (white-padded `fit-inside`, not `cover`).
- `HomeController.Search(keywordId, categoryId, searchString)` already AND-filters correctly; `_CollectionChips.cshtml:15` dropped `categoryId/searchString` → AND felt broken; `Search.cshtml` shows collections row but no category row; `_Pager` correctly preserves 3 params but not `slug`.
- `site.css:955` collections row is plain `overflow-x:auto` with native scrollbar (2010 look), `min-width:6.5rem` chips deform on long names, hover `translateY(-2px)` clipped, dark ships stay `var(--theme-surface) #f7f8fa` on dark page.
- `Details` loads `Category,ProductImages` only — no keyword association surfaced.
- Minimal fixes already on `main` ahead 9: `Keyword.Slug [ValidateNever]+ModelState.Remove`, `_CollectionChips` preserves `categoryId/searchString`, `HomeController.Search` exposes `Categories`, fixed `padding-top`/`width`/`-webkit-line-clamp` + dark `surface-dark #1e2030`. This design builds on that baseline.

## Goals / Non-Goals

**Goals:**
- Slug is user-visible (`/{slug}` in tab/breadcrumb) and bookmarkable (`?keywordId&slug`) without making it the filter key.
- Collection cover appears as a reusable full-bleed hero on `Search?keywordId` with progressive collapse; zero visual debt when missing.
- Both filter rows always visible, additive AND, pagination keeps all params.
- Detail shows its collections as navigable chips.
- Row feels modern without new deps: hidden native bar + fade + vanilla nav buttons, homogeneous chips.

**Non-Goals:**
- No ` /coleccion/{slug}` route in v1 (stay `?keywordId` primary; `slug` is decorative/canonical).
- No change to storage size in this change (keep `1400×500` read; `1920×800 cover-crop` is a noted follow-up).
- No category images row yet (but keep row generic for reuse).
- No sitemap addition for keywords.

## Decisions

### D1 — Slug in URL as decorative param (B)
`Search(string? slug)` alongside `keywordId`. Filter resolves by `keywordId`; if `slug` missing/mismatched vs `Keyword.Slug`, redirect 301 to canonical `?keywordId=&slug=canonical&categoryId=&searchString=&pageNumber`. Razor helpers `asp-route-slug` omitted when null (clean URLs). Rationale: keeps existing `AND` + `_Pager` semantics, old `?keywordId=7` links stay valid, SEO gets readable suffix without FK/routing rewrite.
Alternative: slug as primary key (`/coleccion/{slug}`) — rejected: would need `Slug` lookup + 301 from old ids + route registration for one polish iteration.

### D2 — Hero decision and reusability
Render hero only when `keywordId.HasValue && CoverImageUrl != null` on `Search`. Wrapper `section.collection-hero--breakout` immediately below `header.sticky-top`, outside `div.container` via `width:100vw; margin-left:calc(50% - 50vw)` + `html{overflow-x:clip}` (Windows scrollbar guard). Inside: `<img object-fit:cover center>` + gradient scrim + title/count overlay in safe zone. Heights CSS vars `--hero-h / --hero-h-collapsed`: `400→80` desktop, `340→64` tablet, `260→52` mobile, consistent across collections. Reusable class for future home offer banners (caller passes `imageUrl/title/height`).
Alternative: `background-image` — rejected: non-semantic, no `alt`/`fetchpriority`/`srcset`, iOS `background-attachment:fixed` janky.

### D3 — Progressive collapse (rAF, no lib)
Single `scroll` listener `passive:true` → `requestAnimationFrame` → `progress = clamp(scrollY/(h0-hCollapsed),0,1)` → set CSS vars `--progress`, `--hero-h-current`, `--img-ty = progress*(h0-hCollapsed)*0.5` (parallax), text `opacity=1-progress^1.2`. CSS drives `height` + `transform:translateY` + `opacity` (no layout thrash). Respect `prefers-reduced-motion:reduce` → snap to collapsed/expanded without transition. No `IntersectionObserver` (binary) nor parallax lib.
Risk: `position:sticky` `top` must equal combined navbar height; simpler: hero not sticky, collapses then scrolls away leaving thin strip sticky — we keep hero `position:relative` with JS height, strip remains under navbar.

### D4 — Additive double rows
`Search.cshtml` computes `ViewData["Categories"]` from all visible products (already loaded for `Collections`); collections row above categories row always. Every chip/link does `asp-route-keywordId/ categoryId/ searchString/ slug` preserving the other three (Razor omits null). `active` determined by query id, `aria-current`. `_CollectionChips` partial stays generic (`cleanedCategory/cleanedSearch/cleanedSlug`). Pager includes `slug`.

### D5 — Detail collection chips
Extend `Details` include to `"Category,ProductImages,Keywords.Keyword.Images"` (zero extra query shape — same include family as `Index/Search`). Map `Product.Keywords → CollectionChipVM` and render as small pill/chip row below category badge; each `a asp-action="Search" asp-route-keywordId`. If none → nothing.

### D6 — Modern row without new dependency
Keep `overflow-x:auto` + `scroll-snap-type:x proximity` (ponytail-approved). Enhance: `scrollbar-width:none` + `::-webkit-scrollbar{display:none}`, `scroll-behavior:smooth`, `mask-image: linear-gradient(to right, transparent, black 1rem, black calc(100% - 1rem), transparent)` with `data-at-start/end` toggling fade. Vanilla buttons `scrollBy({left: ±0.8*clientWidth, smooth})`, hide when `scrollLeft<=0` or `scrollLeft+clientWidth>=scrollWidth-1` via `scroll` + `ResizeObserver`. Generic `.scroll-row` class reused by future category images. Long-name `width:6.5rem` + `-webkit-line-clamp:2` already landed; buttons rely on uniform width.

## Risks / Trade-offs

- **Slug mismatch redirect loop** → Mitigation: only redirect when `slug` present and != canonical; canonical built from `Keyword.Slug` `Slugify`-stable; test `Search?k=7&slug=old` → 301 once.
- **Hero LCP cost** (`1400×500` upscaled to `1920` → soft) → Mitigation: `fetchpriority="high"`, `loading="eager"`, `width/height` attrs, keep existing JPEG 75% ~120KB; `1920×800` follow-up will fix blur.
- **White-padded covers** show bars inside `object-fit:cover` → Mitigation: v1 keeps white-padded reads (acceptable); document `1920 cover-crop` as next step, no migration.
- **Breakout `100vw` scrollbar overflow on Windows** → Mitigation: `html{overflow-x:clip}` + `margin-left:calc(50% - 50vw)`.
- **Sticky height coupling** (nav `~106px`) → Mitigation: height via CSS vars; JS reads `offsetHeight` of sticky header at init + `resize`.

## Migration Plan

1. Ship code only (no DB migration): new views/CSS/JS, `CoverImageUrl` exposure uses already-loaded `Images`.
2. Rollback: revert commit; no data loss (hero just disappears, slug param ignored).
3. Follow-up: `KeywordImageService` `cover-crop` to `1920×800` + `1920,1400,960` srcset — separate change, existing `1400×500` rows remain readable.

## Open Questions

None blocking v1. Deferred: focal point `FocalX/Y` for off-center subjects; whether category row later gets image chips (reuse `.scroll-row`).

