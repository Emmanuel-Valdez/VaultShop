## MODIFIED Requirements

### Requirement: Collection hero renders when cover exists
When `Search` is filtered by `keywordId` and the corresponding keyword has a cover crop (`Kind.Cover`, `Kind.CoverMedium` or `Kind.CoverSmall`), the system SHALL render a hero banner immediately below the navbar, breakout to full viewport width, with `object-fit:cover center`, gradient scrim and overlay title/count. The cover SHALL be rendered through a `<picture>` element so the browser can select the crop matching the viewport before any crop of a single source is applied. When the keyword has no cover crop at all, no hero SHALL be rendered.

#### Scenario: Active collection with cover shows hero
- **WHEN** a shopper visits `Search?keywordId=7&slug=naruto` where keyword 7 has a cover crop
- **THEN** a hero `100vw` section appears below the header showing that cover

#### Scenario: No cover shows no hero
- **WHEN** a shopper visits `Search?keywordId=7` where keyword 7 has no cover crop
- **THEN** no hero section is rendered (results start directly below header)

#### Scenario: Home and non-collection searches show no hero
- **WHEN** a shopper visits `Index` or `Search?categoryId=2` without `keywordId`
- **THEN** no hero is rendered

#### Scenario: Cover is exposed as a picture source set
- **WHEN** the hero is rendered for a keyword that has cover crops
- **THEN** the cover is a `<picture>` containing one `<source>` per available crop and an `<img>` fallback, so the viewport-appropriate crop is requested instead of one fixed-ratio image

### Requirement: Hero has consistent responsive heights
The hero SHALL have a consistent fixed height across collections, defined per breakpoint. The height SHALL NOT animate on scroll; scroll motion is limited to image parallax and overlay fade. The height SHALL be expressed through a single CSS custom property per breakpoint so the image, the overlay safe area, and the crop ratios stay derived from one source.

#### Scenario: Desktop heights
- **WHEN** viewed at `>=992px`
- **THEN** the hero height is `320px`

#### Scenario: Tablet height
- **WHEN** viewed between `480px` and `991.98px`
- **THEN** the hero height is `320px`

#### Scenario: Mobile heights
- **WHEN** viewed below `768px`
- **THEN** the hero height is `260px`

#### Scenario: Height is fixed while scrolling
- **WHEN** a shopper scrolls the page while the hero is in view
- **THEN** the hero height remains at its breakpoint value and does not shrink

### Requirement: Hero is reusable
The hero markup and styles SHALL be implementable as a reusable partial with a primary cover image, an optional band-specific cover crop, `title` and `count` inputs for future use (e.g. home offer banners) without collection coupling. A caller that supplies no band-specific crop SHALL still render correctly.

#### Scenario: Reused for non-collection banner
- **WHEN** the component is invoked with a generic image and title (no `keywordId`)
- **THEN** it renders with the same layout and parallax behavior

#### Scenario: Reused without a band-specific crop
- **WHEN** the component is invoked with only a primary image and no additional crop
- **THEN** it renders a single `<img>` with no `<source>` elements and serves that image at every viewport width

## ADDED Requirements

### Requirement: Hero selects the cover crop matching the viewport band
The hero SHALL declare one `<source>` per stored cover crop, each scoped to a viewport band, and SHALL declare the widest available crop as the `<img>` fallback. Bands SHALL be `(max-width:479.98px)`, `(min-width:480px) and (max-width:991.98px)`, and the fallback for wider viewports. Each band's crop ratio SHALL be chosen so that across that band no more than 25% of the image is discarded horizontally and no more than 35% vertically. When a keyword lacks the crop for a band, the browser SHALL fall back to the widest available crop rather than rendering no image.

#### Scenario: Narrow phone receives the small crop uncropped
- **WHEN** a shopper views a collection hero at a `390px` viewport, where the hero box is `390x260` (`1.5:1`)
- **THEN** the `780x520` (`1.5:1`) crop is requested and the full width and height of that crop are visible, with no cropping on either axis

#### Scenario: Smallest supported phone keeps most of the image
- **WHEN** a shopper views a collection hero at a `320px` viewport, where the hero box is `320x260` (`1.23:1`)
- **THEN** the sides of the small crop are cropped and at least 80% of its width remains visible

#### Scenario: Tablet and small laptop receive the medium crop uncropped
- **WHEN** a shopper views a collection hero at a `768px` viewport, where the hero box is `768x320` (`2.4:1`)
- **THEN** the `1200x500` (`2.4:1`) crop is requested and the full width and height of that crop are visible, with no cropping on either axis

#### Scenario: Lower edge of the medium band stays within tolerance
- **WHEN** a shopper views a collection hero at a `480px` viewport, where the hero box is `480x260` (`1.85:1`)
- **THEN** the sides of the medium crop are cropped and no more than 25% of its width is discarded

#### Scenario: Upper edge of the medium band stays within tolerance
- **WHEN** a shopper views a collection hero at a `991px` viewport, where the hero box is `991x320` (`3.1:1`)
- **THEN** the top and bottom of the medium crop are cropped and at least 75% of its height remains visible

#### Scenario: Desktop receives the large crop with full width
- **WHEN** a shopper views a collection hero at a `1280px` viewport, where the hero box is `1280x320` (`4:1`)
- **THEN** the `1600x400` (`4:1`) crop is requested and the full width and height of that crop are visible, with no cropping on either axis

#### Scenario: Very wide desktop bounds vertical loss
- **WHEN** a shopper views a collection hero at a `1920px` viewport, where the hero box is `1920x320` (`6:1`)
- **THEN** the top and bottom of the large crop are cropped symmetrically, discarding no more than 35% of its height in total

#### Scenario: Legacy single cover serves every band
- **WHEN** a keyword has only a `Cover` row uploaded before this change, at any viewport width
- **THEN** no `<source>` element is emitted and that single cover is served at every width, rendering exactly as it did before

#### Scenario: Partial crop set falls back to the widest available
- **WHEN** a keyword has a large crop and a small crop but no medium crop, and the shopper views at a `768px` viewport
- **THEN** the large crop is served rather than no image

### Requirement: Hero parallax and overlay fade on scroll
Scrolling SHALL translate the cover image upward at a fraction of the scroll distance and fade the overlay text out, bounded so the image never travels more than 30% of the hero height and the overlay never fades below full transparency. The effect SHALL be driven by a scroll listener that is passive and scheduled through `requestAnimationFrame`, and SHALL touch only `transform` and `opacity` so it never forces layout. With `prefers-reduced-motion:reduce`, no parallax and no fade SHALL be applied.

#### Scenario: Image parallaxes within a bounded distance
- **WHEN** a shopper scrolls from the top of the document to the bottom of the hero
- **THEN** the cover image has translated upward by at most 30% of the hero height and its full height remains within the hero bounds

#### Scenario: Overlay fades out as the hero leaves view
- **WHEN** a shopper scrolls until the hero is fully scrolled past
- **THEN** the overlay title, slug and count are fully transparent

#### Scenario: Reduced motion respected
- **WHEN** `prefers-reduced-motion:reduce` is set
- **THEN** no parallax translation and no overlay fade occur

## REMOVED Requirements

### Requirement: Hero collapses progressively on scroll
**Reason**: The implementation has not collapsed the hero since the editorial change. `collectionHero.js` performs a compositor-only parallax and overlay fade, and the CSS defines a single fixed `--hero-h` per breakpoint with no `--hero-h-collapsed`. The spec described behavior that does not exist, and this change edits the same requirements, so the drift is corrected here rather than carried forward.

**Migration**: Replace with the ADDED requirement "Hero parallax and overlay fade on scroll". No code change is required; this is a documentation correction that makes the spec match the existing fixed-height implementation.
