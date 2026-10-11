## ADDED Requirements

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