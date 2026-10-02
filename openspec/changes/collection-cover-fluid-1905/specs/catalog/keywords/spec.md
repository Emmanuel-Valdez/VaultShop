## MODIFIED Requirements

### Requirement: Admin keyword CRUD and images
An admin (Admin or Employee role, matching Category) SHALL be able to create, list, edit, and soft-delete keywords through the admin area, including editing `Name`, editing/auto-generating `Slug`, and optionally uploading two independent images per keyword: a **chip image** (used circular in storefront navigation) and a **cover image** (collection hero). The chip image SHALL be stored as a single `400x400` square crop. A cover image SHALL be uploaded once as a single `1905x714` master (ratio `2.67:1`) and SHALL derive its stored crops from it at that same ratio in several pixel sizes (e.g. widths `1905` / `1280` / `768`); the crop rows form a unit — deleting any of them deletes all of them. Cover crops SHALL be produced by center-cropping the master when its ratio differs, never by letterboxing it onto a canvas, and when the master already matches `2.67:1` within tolerance no crop SHALL be applied — only downscale plus re-encode — so a correct master yields full-bleed crops with zero content loss. Both the chip and the cover SHALL be optional, editable independently, and deletable. Image bytes SHALL be stored via the existing storage abstraction (MinIO or Local), only metadata stored in the database; a missing image SHALL NOT prevent creating or saving a keyword and MUST NOT break admin or storefront layout (letter/fallback chip in storefront).

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
- **THEN** three crop rows are created for that keyword at the `2.67:1` ratio in the defined pixel sizes (`1905` / `1280` / `768` wide), each pointing at its own stored object

#### Scenario: Cover master is center-cropped, not letterboxed
- **WHEN** an admin uploads a cover master whose aspect ratio differs from the target `2.67:1` ratio beyond tolerance
- **THEN** each derived crop is fully filled edge to edge with no white bars and no padding

#### Scenario: Correct-ratio master is never cropped
- **WHEN** an admin uploads a cover master whose aspect ratio matches `2.67:1` within tolerance
- **THEN** each derived crop is a pure downscale plus re-encode of the full master with no cropped content

#### Scenario: Replacing a cover removes all previous crops
- **WHEN** an admin uploads a new cover for a keyword that already has crops
- **THEN** the previous crop rows are replaced and their stored objects are deleted, leaving no orphaned objects for that keyword

#### Scenario: Crop rows use a distinct kind per variant
- **WHEN** cover crops are persisted
- **THEN** each size is recorded under its own cover kind so each can be queried individually, while deleting any cover row removes all crop rows and their stored objects together

### Requirement: Cover master dimensions are published in admin
The keyword form SHALL always display, adjacent to the cover file input and without requiring any action from the admin, the recommended master dimensions and the list of crop sizes the system derives from them. The reference SHALL be rendered in the active culture and SHALL name the master size, its aspect ratio, and each derived crop.

#### Scenario: Master dimensions visible next to the cover input
- **WHEN** an admin opens the keyword create or edit form
- **THEN** the text beneath the cover file input states the recommended master size `1905x714`, its `2.67:1` aspect ratio, and the three derived crop sizes

#### Scenario: Reference is localized
- **WHEN** the admin interface is rendered in `es-AR` and in `en-US`
- **THEN** the cover dimension reference is shown translated in each culture

#### Scenario: Reference visible for existing keywords
- **WHEN** an admin edits a keyword created before this change, whose cover predates the single-ratio crops
- **THEN** the same reference is shown and the admin is additionally told that re-uploading the cover will generate the new crops

### Requirement: Cover upload warns on a mismatched master ratio
When an admin selects a cover file whose aspect ratio differs materially from the recommended master ratio, the form SHALL show a non-blocking warning stating that the image will be cropped and that the recommended ratio produces a better result. The warning SHALL NOT block the upload, and the upload SHALL still succeed.

#### Scenario: Correct ratio uploads without warning
- **WHEN** an admin selects a cover file whose aspect ratio matches the recommended `2.67:1` master ratio within 5%
- **THEN** no mismatch warning is shown

#### Scenario: Mismatched ratio warns but does not block
- **WHEN** an admin selects a square cover file
- **THEN** a warning explains the image will be cropped and names the recommended ratio, and the file remains selected and uploadable

#### Scenario: Warning is not announced as an error
- **WHEN** the mismatch warning is displayed
- **THEN** it is presented as advisory text and not as a validation failure
