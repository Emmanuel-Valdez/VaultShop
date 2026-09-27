## MODIFIED Requirements

### Requirement: Admin keyword CRUD and images
An admin (Admin or Employee role, matching Category) SHALL be able to create, list, edit, and soft-delete keywords through the admin area, including editing `Name`, editing/auto-generating `Slug`, and optionally uploading two independent images per keyword: a **chip image** (used circular in storefront navigation) and a **cover image** (collection hero). The chip image SHALL be stored as a single `400x400` square crop. A cover image SHALL be uploaded once as a single master and SHALL derive three stored crops from it — `1600x400` (large), `1200x500` (medium) and `780x520` (small) — each recorded as its own image row; the three crop rows form a unit — deleting any of them deletes all three. Cover crops SHALL be produced by center-cropping the master, never by letterboxing it onto a canvas, so a master of any aspect ratio yields full-bleed crops without white bars. Both the chip and the cover SHALL be optional, editable independently, and deletable. Image bytes SHALL be stored via the existing storage abstraction (MinIO or Local), only metadata stored in the database; a missing image SHALL NOT prevent creating or saving a keyword and MUST NOT break admin or storefront layout (letter/fallback chip in storefront).

#### Scenario: Keyword created without any image
- **WHEN** an admin creates a keyword with only `Name` and `Slug`
- **THEN** the keyword is saved successfully with no chip and no cover, and the storefront renders its fallback chip

#### Scenario: Chip and cover managed independently
- **WHEN** an admin uploads a chip image, then later uploads a cover image, then replaces the chip image
- **THEN** each image updates independently without affecting the other, and both previews in admin reflect their current values

#### Scenario: Image bytes stored out of database
- **WHEN** a keyword image is saved
- **THEN** the binary content is written through the storage abstraction (S3/MinIO or local filesystem) and the database row stores URL/key/metadata (content type, size, provider) following the existing `ProductImage` pattern

#### Scenario: One cover upload produces three crops
- **WHEN** an admin uploads a cover image for a keyword
- **THEN** three image rows are created for that keyword at `1600x400`, `1200x500` and `780x520`, each pointing at its own stored object

#### Scenario: Cover master is center-cropped, not letterboxed
- **WHEN** an admin uploads a cover master whose aspect ratio differs from every target crop
- **THEN** each derived crop is fully filled edge to edge with no white bars and no padding

#### Scenario: Replacing a cover removes all previous crops
- **WHEN** an admin uploads a new cover for a keyword that already has crops
- **THEN** the previous crop rows are replaced and their stored objects are deleted, leaving no orphaned objects for that keyword

#### Scenario: Crop rows use a distinct kind per variant
- **WHEN** cover crops are persisted
- **THEN** the large crop is recorded under the large-cover kind, the medium crop under a medium-cover kind, and the small crop under a small-cover kind, so each can be queried individually, while deleting any cover row removes all three rows and their stored objects together

## ADDED Requirements

### Requirement: Cover master dimensions are published in admin
The keyword form SHALL always display, adjacent to the cover file input and without requiring any action from the admin, the recommended master dimensions and the list of crop sizes the system derives from them. The reference SHALL be rendered in the active culture and SHALL name the master size, its aspect ratio, and each derived crop.

#### Scenario: Master dimensions visible next to the cover input
- **WHEN** an admin opens the keyword create or edit form
- **THEN** the text beneath the cover file input states the recommended master size `1920x1080`, its `16:9` aspect ratio, and the three derived crop sizes

#### Scenario: Reference is localized
- **WHEN** the admin interface is rendered in `es-AR` and in `en-US`
- **THEN** the cover dimension reference is shown translated in each culture

#### Scenario: Reference visible for existing keywords
- **WHEN** an admin edits a keyword created before this change, whose cover predates the responsive crops
- **THEN** the same reference is shown and the admin is additionally told that re-uploading the cover will generate the responsive crops

### Requirement: Cover upload warns on a mismatched master ratio
When an admin selects a cover file whose aspect ratio differs materially from the recommended master ratio, the form SHALL show a non-blocking warning stating that the image will be cropped and that the recommended ratio produces a better result. The warning SHALL NOT block the upload, and the upload SHALL still succeed.

#### Scenario: Correct ratio uploads without warning
- **WHEN** an admin selects a cover file whose aspect ratio matches the recommended master ratio
- **THEN** no mismatch warning is shown

#### Scenario: Mismatched ratio warns but does not block
- **WHEN** an admin selects a square cover file
- **THEN** a warning explains the image will be cropped and names the recommended ratio, and the file remains selected and uploadable

#### Scenario: Warning is not announced as an error
- **WHEN** the mismatch warning is displayed
- **THEN** it is presented as advisory text and not as a validation failure
