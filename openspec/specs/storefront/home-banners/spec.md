## Purpose

Provides admin-managed promotional banners for the storefront home page: an ordered, activatable set of image-and-text slides rendered as a full-bleed autoplaying carousel, with per-banner layout archetypes, a legibility-safe text-color contract, internal-only link targets, and a reusable Skia/MinIO image pipeline.

## ADDED Requirements

### Requirement: Admin-managed banner records

The system SHALL let an administrator create, edit, activate/deactivate, delete, and order `Banner` records. Each banner SHALL store: a layout archetype, a text color choice, an optional title, hook, body and call-to-action text, an alt-text value, a link target, an integer sort order, and an active flag.

#### Scenario: Create a banner
- **WHEN** an administrator submits a valid banner with a title, a link target, and a landscape image
- **THEN** the banner is persisted and becomes eligible to render on the home page when active

#### Scenario: Edit a banner without changing the image
- **WHEN** an administrator edits the text, layout, color, link, sort order or active flag and posts no new image file
- **THEN** the update is saved and the existing image set is preserved unchanged

#### Scenario: Delete a banner
- **WHEN** an administrator deletes a banner
- **THEN** the banner is removed and its stored image objects are cleaned up from storage, best-effort after the database change succeeds

#### Scenario: Validation failure leaves the banner untouched
- **WHEN** an administrator submits a banner whose image upload fails validation
- **THEN** the record is not created or modified and a localized validation error is shown

### Requirement: Activation is a boolean with no scheduling

A banner SHALL be eligible to render based only on its `IsActive` flag; the system SHALL NOT apply date or time windows to banner visibility.

#### Scenario: Only active banners render
- **WHEN** the home page renders and some banners have `IsActive=false`
- **THEN** only banners with `IsActive=true` are considered for the carousel

### Requirement: Banner layout archetypes and responsive reflow

Each banner SHALL declare one layout archetype: `Crossed` or `ImageLed`. Both SHALL place sectioned text over a full-bleed image. `Crossed` SHALL show the title and call-to-action on the left with the hook on the right on wide screens, and a centered stacked order (title, hook, call-to-action) on narrow screens. `ImageLed` SHALL show a left-aligned text rail with the image subject on the right on wide screens, and on narrow screens overlay the text in a left rail with the title at the top-left and the call-to-action pinned to the bottom-left.

#### Scenario: Crossed desktop composition
- **WHEN** a `Crossed` banner renders at a viewport width of 992px or greater
- **THEN** the title and call-to-action appear in a left column and the hook appears in a right column, both over the image

#### Scenario: Crossed mobile reflow
- **WHEN** a `Crossed` banner renders below 992px
- **THEN** the title, hook and call-to-action are stacked vertically and centered, with the call-to-action near full width at the bottom

#### Scenario: ImageLed desktop composition
- **WHEN** an `ImageLed` banner renders at 992px or greater
- **THEN** the text sits in a left-aligned rail with left padding over the image and the image subject remains visible on the right

#### Scenario: ImageLed mobile overlay
- **WHEN** an `ImageLed` banner renders below 992px
- **THEN** the text overlays the image with the title at the top-left and the call-to-action pinned to the bottom-left, without covering the subject

### Requirement: Sectioned overlay text

The overlay text SHALL be composed of independent sections: title, hook, body, and call-to-action. Each section SHALL render only when its text is non-empty. The sections SHALL be rendered as visually-styled inline containers and SHALL NOT emit heading elements, so the document heading order is not altered and inactive carousel slides do not introduce duplicate headings.

#### Scenario: Empty sections are omitted
- **WHEN** a banner has a title and a call-to-action but no hook or body text
- **THEN** only the title and call-to-action render, and the layout adapts without empty gaps

#### Scenario: No heading elements in the overlay
- **WHEN** any banner slide is rendered
- **THEN** its title and hook are not emitted as `h1`–`h6` elements

### Requirement: Text color and contrast contract

A banner SHALL have a single text-color choice from `BrandLight`, `BrandDark`, `White`, and `Black`. That single choice SHALL deterministically set both the overlay text color and a contrast scrim whose polarity opposes the text color, so the two can never be mismatched. The rendered combination SHALL meet WCAG AA contrast (at least 4.5:1 for body text and 3:1 for large text) over arbitrary admin-supplied images.

#### Scenario: Light text over a light image stays legible
- **WHEN** a banner uses a light text color and the underlying image is light
- **THEN** a dark scrim is applied behind the text so the text meets the AA contrast threshold

#### Scenario: Dark text over a dark image stays legible
- **WHEN** a banner uses a dark text color and the underlying image is dark
- **THEN** a light scrim is applied behind the text so the text meets the AA contrast threshold

#### Scenario: ImageLed scrim preserves the subject
- **WHEN** an `ImageLed` banner renders with light text
- **THEN** the contrast scrim is anchored to the left text rail and fades out before the subject region, keeping the subject visually clean

### Requirement: Internal-only link targets

A banner link SHALL be defined by an internal target only: a target kind of `Store`, `Category`, `Collection`, or `Product`, optionally with the target entity identifier. The system SHALL resolve the destination route server-side from the target entity and SHALL NOT store or accept a raw URL. When the target entity is missing or soft-deleted, the slide SHALL render without a link rather than a broken or redirecting link.

#### Scenario: Category target resolves server-side
- **WHEN** a banner targets a category
- **THEN** the rendered link points to the canonical filtered catalog route for that category, including its slug

#### Scenario: No link target
- **WHEN** a banner has the `Store` target with no entity identifier
- **THEN** the rendered link points to the store home page

#### Scenario: Deleted target renders unlinked
- **WHEN** a banner's target entity no longer exists or is soft-deleted
- **THEN** the slide renders the image and text without an anchor element

### Requirement: Banner image pipeline

A banner SHALL require a landscape cover image, which the system SHALL validate (file type, size, decodable) and use to generate a density set — `Master`, `Medium`, and `Small` — at a single landscape ratio, without ever upscaling; masters too small for a larger variant SHALL yield only the variants they can cover. Each banner SHALL optionally accept a separate mobile asset at a portrait ratio (4:5) stored as the `Mobile` image. All images SHALL be stored via the object-storage service. When a mobile asset exists, the home carousel SHALL serve it below 992px via art-direction; when it does not exist, the landscape cover SHALL be cropped or zoomed with CSS to fill the mobile box.

#### Scenario: Undersized cover is rejected or produces a partial set
- **WHEN** an administrator uploads a cover smaller than the smallest supported variant
- **THEN** the upload is rejected with a localized error
- **WHEN** the cover is large enough for the small variant but not the large one
- **THEN** only the variants that can be produced without upscaling are stored

#### Scenario: Optional mobile asset is served on narrow screens
- **WHEN** a banner has a `Mobile` image and the viewport is below 992px
- **THEN** the mobile image is served instead of the landscape density set

#### Scenario: Missing mobile asset falls back to a CSS crop
- **WHEN** a banner has no `Mobile` image and the viewport is below 992px
- **THEN** the landscape cover is cropped or zoomed with CSS to fill the mobile box without a broken or blank image

#### Scenario: Mobile asset required only when supplied
- **WHEN** an administrator saves a banner of either layout and does not upload a mobile asset
- **THEN** the save succeeds; the mobile asset is never required

### Requirement: Home carousel rendering

The home page SHALL render the active banners as a full-bleed carousel at the top of the page, ordered by ascending sort order with identifier as tiebreaker. The carousel SHALL replace the current static store-name/value-banner hero block on the home page. Navigation controls (indicators, previous, next) SHALL render only when more than one banner is active. When no banner is active, the carousel SHALL render nothing and the home page SHALL remain otherwise unchanged.

#### Scenario: Multiple active banners show controls
- **WHEN** two or more banners are active
- **THEN** the carousel renders indicators and previous/next controls in addition to the slides

#### Scenario: Single banner has no controls
- **WHEN** exactly one banner is active
- **THEN** the carousel renders the single slide with no indicators and no previous/next controls

#### Scenario: No active banners
- **WHEN** no banner is active
- **THEN** no carousel markup is rendered on the home page

#### Scenario: Ordering follows sort order
- **WHEN** banners with different sort orders are active
- **THEN** the slides render in ascending sort order

### Requirement: Carousel autoplay and accessibility

The carousel SHALL autoplay at a 6000 ms interval only when more than one banner is active. It SHALL provide a visible pause/play control, pause on hover and focus, and SHALL NOT autoplay when the user prefers reduced motion. Each slide SHALL expose an accessible name and role-description, its image SHALL carry the banner alt text (falling back to the title), the whole slide SHALL be a single focusable link when a target exists, and the call-to-action SHALL NOT be a second nested interactive element.

#### Scenario: Reduced motion disables autoplay
- **WHEN** the user's environment prefers reduced motion
- **THEN** the carousel does not autoplay, while manual navigation remains available

#### Scenario: Pause control stops autoplay
- **WHEN** the user activates the pause control
- **THEN** autoplay stops until the user resumes it

#### Scenario: Single slide link and alt text
- **WHEN** a slide with a target and an image renders
- **THEN** the slide contains one focusable anchor wrapping the image and text, the image's alt text equals the banner alt text or title, and the call-to-action is a non-interactive styled element inside that anchor

### Requirement: Banner safe zone

A `Banner` record SHALL support an optional safe zone defined by four normalized coordinates — `left`, `top`, `width`, `height` — each in `[0,1]` relative to the cover image. The four coordinates SHALL be stored together (all set or none; a partial set SHALL be rejected with a localized validation error and MUST NOT persist). A stored safe zone SHALL apply proportionally to both the landscape cover composition and the portrait mobile composition (art-directed or CSS-cropped fallback). When a safe zone is set, the storefront SHALL confine every overlay text section (title, hook, body, call-to-action) inside that rectangle; when none is set, the overlay SHALL use the layout's fixed rail and padding geometry unchanged. The text color and contrast-scrim contract SHALL NOT be altered by the presence of a safe zone.

#### Scenario: Safe zone confines overlay text
- **WHEN** a banner defines a safe zone and renders on the home page
- **THEN** all overlay text sections are positioned inside the safe-zone rectangle and no overlay text extends outside it

#### Scenario: No safe zone keeps the fixed geometry
- **WHEN** a banner has no safe zone stored
- **THEN** its overlay renders with the standard fixed rail/padding geometry of its layout, unchanged from banners without safe-zone support

#### Scenario: Safe zone applies proportionally to mobile art
- **WHEN** a banner defines a safe zone and renders below 992px using the mobile asset or the CSS-cropped cover
- **THEN** the overlay is confined inside the same normalized rectangle scaled to the mobile box, and no overlay text extends outside it

#### Scenario: Partial safe zone is rejected
- **WHEN** an administrator submits a banner with fewer than four safe-zone coordinates, or values outside `[0,1]`, or coordinates that exceed the image bounds (`left + width > 1` or `top + height > 1`)
- **THEN** the record is not created or modified and a localized validation error is shown

### Requirement: Live preview and safe-zone editing

The banner admin form SHALL render a live preview of the banner from the current form state — title, hook, body, call-to-action, layout, text color, and image — for both new and existing banners, without a page reload or a server round trip for a just-chosen file. The preview SHALL offer a desktop (landscape) and mobile (portrait) composition, and its image selection SHALL mirror the storefront's layout-specific art-direction (the mobile asset applies to the ImageLed layout; Crossed shows the cover cropped). The administrator SHALL be able to draw or resize a single rectangle over the desktop preview to define the safe zone; the same rectangle SHALL be shown proportionally over the mobile preview, and the four normalized coordinates SHALL be written to the form so they persist on save.

#### Scenario: Preview reflects live form edits
- **WHEN** an administrator changes text, layout, color, or image in the form
- **THEN** the preview updates immediately to the latest form state without reloading the page

#### Scenario: Preview shows a just-chosen file for a new banner
- **WHEN** an administrator picks a cover or mobile image for a banner that is not yet saved
- **THEN** the preview renders that image immediately, with no prior save required

#### Scenario: Safe zone drawn over the desktop preview persists
- **WHEN** an administrator draws or resizes the safe-zone rectangle over the desktop preview and saves the banner
- **THEN** the resulting normalized coordinates are stored with the banner and confine the rendered overlay

#### Scenario: Safe zone mirrors on the mobile preview
- **WHEN** a safe zone is set and the administrator switches the preview to the mobile composition
- **THEN** the rectangle is displayed at the same proportional position and size over the mobile preview
