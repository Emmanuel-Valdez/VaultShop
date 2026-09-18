## Purpose

Provides a reusable visual hero for collection cover images that introduces the collection with a full-bleed image above the search results.

## ADDED Requirements

### Requirement: Collection hero renders when cover exists
When `Search` is filtered by `keywordId` and the corresponding keyword has a `Cover` image (`Kind.Cover`), the system SHALL render a hero banner immediately below the navbar, breakout to full viewport width, with `object-fit:cover center`, gradient scrim and overlay title/count. When no cover exists, no hero SHALL be rendered.

#### Scenario: Active collection with cover shows hero
- **WHEN** a shopper visits `Search?keywordId=7&slug=naruto` where keyword 7 has a cover image
- **THEN** a hero `100vw` section appears below the header showing that image cover-cropped

#### Scenario: No cover shows no hero
- **WHEN** a shopper visits `Search?keywordId=7` where keyword 7 has no cover
- **THEN** no hero section is rendered (results start directly below header)

#### Scenario: Home and non-collection searches show no hero
- **WHEN** a shopper visits `Index` or `Search?categoryId=2` without `keywordId`
- **THEN** no hero is rendered

### Requirement: Hero has consistent responsive heights
The hero SHALL have consistent heights across collections, with defined collapsed and expanded values per breakpoint.

#### Scenario: Desktop heights
- **WHEN** viewed on desktop `>=992px`
- **THEN** expanded height is `~400px` and collapsed strip `~72-84px`

#### Scenario: Mobile heights
- **WHEN** viewed on mobile `<768px`
- **THEN** expanded height is `~260px` and collapsed strip `~48-52px`

### Requirement: Hero collapses progressively on scroll
Scrolling down SHALL progressively shrink the hero height and parallax the image (about `0.5×` scroll) with smooth transition; the overlay text fades. With `prefers-reduced-motion:reduce`, the collapse SHALL be instant without animation and no parallax.

#### Scenario: Progressive collapse while scrolling
- **WHEN** a shopper scrolls from top `0` to `heroH - collapsedH`
- **THEN** height animates from expanded to collapsed and image translates up proportionally

#### Scenario: Collapsed strip remains visible
- **WHEN** scroll exceeds the threshold
- **THEN** a thin strip of the image remains visible under the navbar

#### Scenario: Reduced motion respected
- **WHEN** `prefers-reduced-motion:reduce` is set
- **THEN** no smooth scroll animation or parallax occurs

### Requirement: Hero is reusable
The hero markup and styles SHALL be implementable as a reusable partial with `imageUrl`, `title`, `count` inputs for future use (e.g., home offer banners) without collection coupling.

#### Scenario: Reused for non-collection banner
- **WHEN** the component is invoked with a generic image and title (no `keywordId`)
- **THEN** it renders with the same layout and collapse behavior

