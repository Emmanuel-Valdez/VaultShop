## Purpose

Provides a reusable visual hero for collection cover images that introduces the collection with a full-bleed image above the search results.

## Requirements

### Requirement: Collection hero renders when cover exists
When `Search` is filtered by `keywordId` and the corresponding keyword has a cover crop (`Kind.Cover`, `Kind.CoverMedium` or `Kind.CoverSmall`), the system SHALL render a hero banner immediately below the navbar, breakout to full viewport width, with `object-fit:cover center`, gradient scrim and overlay title/count. The cover SHALL be rendered through a single `<img>` with a single-ratio `srcset` so the browser selects pixel density — never a different ratio. When the keyword has no cover crop at all, no hero SHALL be rendered.

#### Scenario: Active collection with cover shows hero
- **WHEN** a shopper visits `Search?keywordId=7&slug=naruto` where keyword 7 has a cover crop
- **THEN** a hero `100vw` section appears below the header showing that cover

#### Scenario: No cover shows no hero
- **WHEN** a shopper visits `Search?keywordId=7` where keyword 7 has no cover crop
- **THEN** no hero section is rendered (results start directly below header)

#### Scenario: Home and non-collection searches show no hero
- **WHEN** a shopper visits `Index` or `Search?categoryId=2` without `keywordId`
- **THEN** no hero is rendered

#### Scenario: Cover is exposed as a single-ratio source set
- **WHEN** the hero is rendered for a keyword that has cover crops
- **THEN** the cover is a single `<img>` with a `srcset` of same-ratio widths (e.g. `1905w` / `1280w` / `768w`), so the browser selects pixel density instead of one fixed-size image

### Requirement: Hero has consistent responsive heights
The hero SHALL have a fluid height derived from a single master ratio with no clamping: `height = 100vw / 2.67`, where `2.67` is the `1905x714` master ratio. The box ratio equals the image ratio at every viewport width, so no cropping occurs on either axis — from ~120px tall at `320px` to ~719px tall at `1920px`. There are no rails: any pinned height would force cropping, which is ruled out as unpredictable for the banner designer. The accepted consequences are a short strip on very narrow phones and a tall hero on very wide desktops. The height SHALL NOT animate on scroll; scroll motion is limited to image parallax and overlay fade.

#### Scenario: Desktop heights
- **WHEN** viewed at `>=992px`
- **THEN** the hero height is `100vw / 2.67` (`371px` at `992px` up to `719px` at `1920px`) with the full image visible and no cropping on either axis

#### Scenario: Tablet height
- **WHEN** viewed between `480px` and `991.98px`
- **THEN** the hero height is `100vw / 2.67` (`180px` to `371px`) with the full image visible and no cropping on either axis

#### Scenario: Mobile heights
- **WHEN** viewed below `768px`
- **THEN** the hero height is `100vw / 2.67` (`~120px` at `320px`, `~146px` at `390px`) with the full image visible and no cropping on either axis, and the title + count overlay stays legible

#### Scenario: Height is fixed while scrolling
- **WHEN** a shopper scrolls the page while the hero is in view
- **THEN** the hero height remains at its computed value and does not shrink

#### Scenario: In-band widths show zero crop
- **WHEN** viewed at any viewport width from `320px` to `1920px`
- **THEN** the hero box ratio equals the image ratio and the full image is visible with no cropping on either axis

#### Scenario: Very wide desktop pins at the upper rail
- **WHEN** viewed at `1920px`
- **THEN** the hero height is `~719px` with the full image visible and no cropping on either axis; the catalog sits below the tall hero (accepted consequence, not a defect)

### Requirement: Hero is reusable
The hero markup and styles SHALL be implementable as a reusable partial with a primary cover image, an optional band-specific cover crop, `title` and `count` inputs for future use (e.g. home offer banners) without collection coupling. A caller that supplies no band-specific crop SHALL still render correctly.

#### Scenario: Reused for non-collection banner
- **WHEN** the component is invoked with a generic image and title (no `keywordId`)
- **THEN** it renders with the same layout and parallax behavior

#### Scenario: Reused without a band-specific crop
- **WHEN** the component is invoked with only a primary image and no additional crop
- **THEN** it renders a single `<img>` with no `<source>` elements and serves that image at every viewport width

### Requirement: Hero selects the cover crop matching the viewport band
The hero SHALL declare a single-ratio responsive image set: one `<img>` with `srcset` widths derived from the same `1905x714` (2.67:1) master (e.g. `1905w` / `1280w` / `768w` with matching `sizes`), so the browser selects pixel density — never a different ratio. Viewport bands no longer select different crops; density selection replaces band selection. When a keyword carries only legacy pre-change crops (`Kind.Cover`, `Kind.CoverMedium`, `Kind.CoverSmall`), the hero SHALL fall back to serving the widest available legacy crop as a single `<img>` with no `<source>` elements, rendering exactly as before, until the cover is re-uploaded.

#### Scenario: Narrow phone receives the small crop uncropped
- **WHEN** a shopper views a collection hero at a `390px` viewport, where the hero box is `390x146` (`2.67:1`)
- **THEN** the `768`-wide variant is requested and the full width and height of the image are visible, with no cropping on either axis

#### Scenario: Smallest supported phone keeps most of the image
- **WHEN** a shopper views a collection hero at a `320px` viewport, where the hero box is `320x120` (`2.67:1`)
- **THEN** the full width and height of the image are visible, with no cropping on either axis, and the title + count overlay stays legible on the short strip

#### Scenario: Tablet and small laptop receive the medium crop uncropped
- **WHEN** a shopper views a collection hero at a `768px` viewport, where the hero box is `768x288` (`2.67:1`)
- **THEN** the `768`-wide variant is requested and the full width and height of the image are visible, with no cropping on either axis

#### Scenario: Lower edge of the medium band stays within tolerance
- **WHEN** a shopper views a collection hero at a `480px` viewport, where the hero box is `480x180` (`2.67:1`)
- **THEN** the full width and height of the image are visible, with no cropping on either axis

#### Scenario: Upper edge of the medium band stays within tolerance
- **WHEN** a shopper views a collection hero at a `991px` viewport, where the hero box is `991x371` (`2.67:1`)
- **THEN** the full width and height of the image are visible, with no cropping on either axis

#### Scenario: Desktop receives the large crop with full width
- **WHEN** a shopper views a collection hero at a `1280px` viewport, where the hero box is `1280x479` (`2.67:1`)
- **THEN** the `1280`-wide variant is requested and the full width and height of the image are visible, with no cropping on either axis

#### Scenario: Very wide desktop bounds vertical loss
- **WHEN** a shopper views a collection hero at a `1920px` viewport, where the hero box is `1920x719` (`2.67:1`)
- **THEN** the full width and height of the image are visible, with no cropping on either axis

#### Scenario: Legacy single cover serves every band
- **WHEN** a keyword has only a `Cover` row uploaded before this change, at any viewport width
- **THEN** no `srcset` variant set is emitted and that single cover is served at every width, rendering exactly as it did before

#### Scenario: Partial crop set falls back to the widest available
- **WHEN** a keyword has legacy crops but no new variant set, and the shopper views at a `768px` viewport
- **THEN** the widest available legacy crop is served rather than no image

### Requirement: Hero overlay shows title and count without slug
The hero overlay SHALL render the collection title and product count only; the slug SHALL NOT be rendered.

#### Scenario: Collection overlay has no slug
- **WHEN** a shopper views a collection hero
- **THEN** the title and the product count are visible and no slug text is rendered

#### Scenario: Reused banner has no slug
- **WHEN** the component is invoked with a generic image and title (no `keywordId`)
- **THEN** it renders the title (and the count when provided) with no slug text

### Requirement: Hero parallax and overlay fade on scroll
Scrolling SHALL translate the cover image upward at a fraction of the scroll distance and fade the overlay text out, bounded so the image never travels more than 30% of the hero height and the overlay never fades below full transparency. The effect SHALL be driven by a scroll listener that is passive and scheduled through `requestAnimationFrame`, and SHALL touch only `transform` and `opacity` so it never forces layout. With `prefers-reduced-motion:reduce`, no parallax and no fade SHALL be applied.

#### Scenario: Image parallaxes within a bounded distance
- **WHEN** a shopper scrolls from the top of the document to the bottom of the hero
- **THEN** the cover image has translated upward by at most 30% of the hero height and its full height remains within the hero bounds

#### Scenario: Overlay fades out as the hero leaves view
- **WHEN** a shopper scrolls until the hero is fully scrolled past
- **THEN** the overlay title and count are fully transparent

#### Scenario: Reduced motion respected
- **WHEN** `prefers-reduced-motion:reduce` is set
- **THEN** no parallax translation and no overlay fade occur
