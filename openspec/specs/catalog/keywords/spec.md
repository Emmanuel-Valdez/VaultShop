## Purpose

Adds a transversal Keyword/Collection layer to the catalog: products get tagged with multiple thematic Keywords that shoppers browse as a visual discovery row, filter by exact ID, and combine with category and text search. Keywords stay fully decoupled from Category pricing, packaging, and hierarchy.

## Requirements

### Requirement: Keyword model with transitive tagging
The system SHALL maintain a `Keyword` entity (`Id`, `Name`, `Slug`, `IsDeleted`) and a many-to-many `ProductKeyword` association (`ProductId` + `KeywordId` composite primary key). A product SHALL belong to exactly one Category as today, and SHALL be able to carry zero or more Keywords. Duplicate `(ProductId, KeywordId)` associations SHALL be impossible at the database level.

#### Scenario: Product carries multiple keywords
- **WHEN** a product is associated with keyword A and keyword B
- **THEN** both associations exist simultaneously with no duplicate rows

#### Scenario: Duplicate association blocked
- **WHEN** an attempt is made to associate the same `(ProductId, KeywordId)` pair twice
- **THEN** the database rejects the second insert (composite primary key)

#### Scenario: Product without keywords
- **WHEN** a product has no keywords assigned
- **THEN** the product behaves exactly as today — the category remains its only classification

### Requirement: Slug for active keywords is unique
Each `Keyword` SHALL have a `Slug` (e.g. `Naruto` → `naruto`, `Studio Ghibli` → `studio-ghibli`). The slug SHALL be unique among **active** (non-deleted) keywords via a partial unique index on `Slug WHERE IsDeleted = false`. A soft-deleted keyword SHALL NOT block another keyword from reusing its slug. When a keyword is created without a slug, the system SHALL derive one from `Name`.

#### Scenario: Slug auto-generated from name
- **WHEN** a keyword is created with `Name = "My Hero Academia"` and no slug
- **THEN** the stored slug is `my-hero-academia`

#### Scenario: Duplicate active slug rejected
- **WHEN** a second active keyword is created with a slug equal to an existing active keyword's slug
- **THEN** the creation is rejected with a validation error

#### Scenario: Reusing a soft-deleted keyword's slug
- **WHEN** a keyword with slug `naruto` is soft-deleted, then another keyword with slug `naruto` is created
- **THEN** the new keyword is accepted because the partial index only covers active keywords

### Requirement: Keyword soft delete keeps associations valid
Deleting a keyword SHALL be a soft delete (`IsDeleted = true`), following the Category pattern. Deleting SHALL be blocked when the keyword is referenced by at least one active product; the response SHALL carry a localized message explaining that products must first be unassigned. Keyword deletion SHALL NOT hard-delete `ProductKeyword` rows in a way that leaves dangling references.

#### Scenario: Delete blocked with referenced products
- **WHEN** an admin attempts to delete a keyword referenced by one or more active (`!IsDeleted`) products
- **THEN** the delete is refused, the keyword stays active, and the admin sees a localized error explaining the block

#### Scenario: Delete succeeds when unreferenced
- **WHEN** an admin deletes a keyword with no active product references (or only soft-deleted product references)
- **THEN** the keyword is soft-deleted and the `ProductKeyword` rows for it are removed cleanly without breaking product integrity

### Requirement: Admin keyword CRUD and images
An admin (Admin or Employee role, matching Category) SHALL be able to create, list, edit, and soft-delete keywords through the admin area, including editing `Name`, editing/auto-generating `Slug`, and optionally uploading two independent images per keyword: a **chip image** (used circular in storefront navigation) and a **cover image** (collection hero). The chip image SHALL be stored as a single `400x400` square crop. A cover image SHALL be uploaded once as a single master and SHALL derive three stored crops from it — `1600x700` (large), `1200x500` (medium) and `780x520` (small) — each recorded as its own image row; the three crop rows form a unit — deleting any of them deletes all three. Cover crops SHALL be produced by center-cropping the master, never by letterboxing it onto a canvas, so a master of any aspect ratio yields full-bleed crops without white bars. Both the chip and the cover SHALL be optional, editable independently, and deletable. Image bytes SHALL be stored via the existing storage abstraction (MinIO or Local), only metadata stored in the database; a missing image SHALL NOT prevent creating or saving a keyword and MUST NOT break admin or storefront layout (letter/fallback chip in storefront).

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
- **THEN** three image rows are created for that keyword at `1600x700`, `1200x500` and `780x520`, each pointing at its own stored object

#### Scenario: Cover master is center-cropped, not letterboxed
- **WHEN** an admin uploads a cover master whose aspect ratio differs from every target crop
- **THEN** each derived crop is fully filled edge to edge with no white bars and no padding

#### Scenario: Replacing a cover removes all previous crops
- **WHEN** an admin uploads a new cover for a keyword that already has crops
- **THEN** the previous crop rows are replaced and their stored objects are deleted, leaving no orphaned objects for that keyword

#### Scenario: Crop rows use a distinct kind per variant
- **WHEN** cover crops are persisted
- **THEN** the large crop is recorded under the large-cover kind, the medium crop under a medium-cover kind, and the small crop under a small-cover kind, so each can be queried individually, while deleting any cover row removes all three rows and their stored objects together

### Requirement: Storefront collection discovery row
The storefront home page SHALL render a Collection/Keyword discovery section near the existing "Shop by Category" section, as a single horizontal row of items — each item an optional circular chip image (or fallback) with the keyword name below — that scrolls horizontally on overflow. On mobile the row SHALL NOT wrap into multiple rows; items SHALL have adequate touch targets and the row SHALL support horizontal scrolling without broken layout. The section SHALL render only when at least one keyword exists that is active and referenced by at least one visible product.

#### Scenario: Desktop collection row
- **WHEN** active keywords exist and the storefront home page renders
- **THEN** a horizontal row of collection chips appears near Shop by Category with each chip showing its image (or fallback) and name

#### Scenario: Mobile single-row scroll
- **WHEN** the storefront home page is viewed on a narrow/mobile viewport with many keywords
- **THEN** chips stay on one row and overflow scrolls horizontally with touch; no chip is wrapped to a second row and no name breaks the layout

#### Scenario: No keywords available
- **WHEN** no active keyword is referenced by any visible product
- **THEN** the collection section is not rendered and the page looks as before

### Requirement: Exact keyword filtering combined with existing filters
Clicking a collection chip SHALL filter by the exact keyword ID (e.g. `?keywordId=N`), never by a textual match on the keyword name. The keyword filter SHALL combine with the category filter and the text `searchString` using AND: a result SHALL match all three simultaneously when all are set. Text search semantics SHALL remain unchanged. The active collection SHALL be visually marked and SHALL expose a control to remove only the keyword filter, preserving the other active filters.

#### Scenario: Chip filters by exact keyword id
- **WHEN** a shopper clicks the "Naruto" chip
- **THEN** the app shows products whose keyword associations include Naruto, identified by its ID (no name/description/category text matching involved)

#### Scenario: Combined AND filtering
- **WHEN** a shopper navigates with `categoryId = Mochilas`, `keywordId = 7` (Naruto), and `searchString = negra`
- **THEN** the result contains only products that belong to Mochilas, are tagged Naruto, and match "negra" in text search

#### Scenario: Active chip and removal
- **WHEN** the shopper is viewing keyword-filtered results in the storefront
- **THEN** the corresponding chip is visually active and offers a "remove keyword" control that drops only the keyword filter while keeping category and text-search filters
- **AND** the removal keeps the shopper on the results page (does not reset to home)

### Requirement: Pagination preserves filters
Pagination links SHALL preserve the current `keywordId`, `categoryId`, and `searchString` values on every page link.

#### Scenario: Filters survive page change
- **WHEN** a shopper pages from page 1 to page 2 while `keywordId=7` and `categoryId=2` are active
- **THEN** the page-2 link carries `keywordId=7`, `categoryId=2`, and any active `searchString`

#### Scenario: No filters renders w/o extras
- **WHEN** no filters are active
- **THEN** pagination links contain no filter query parameters

### Requirement: Product counter per keyword
Each keyword SHALL have an optional product counter equal to the number of **distinct** available products associated with it, counting only products that are not deleted, are available in store (`IsAvailableInStore`), and have `StockQuantity > 0`. The counter SHALL be computed in a single aggregated pass for all visible keywords (no per-keyword query).

#### Scenario: Counter counts distinct available in-stock products
- **WHEN** a keyword is associated with 26 non-deleted, store-available products of which 2 have `StockQuantity = 0`
- **THEN** the counter shows 24

#### Scenario: Counts computed without N+1
- **WHEN** the storefront home page renders the collection section with N keywords
- **THEN** product counts for all N keywords come from a single aggregated query, not one query per keyword

### Requirement: Reserved CategoryId without behavior
The `Keyword` entity SHALL expose a nullable `CategoryId` reserved exclusively for a possible future Category→Keyword relationship. In v1 it SHALL be a plain nullable column with no foreign key, no navigation, no admin input, no validation, and no effect on keyword listing, filtering, product assignment, Category behavior, packaging, costs, or pricing.

#### Scenario: CategoryId never drives behavior in v1
- **WHEN** keywords exist with or without a `CategoryId` value
- **THEN** every storefront and admin behavior is identical regardless of that value (it is never read for filters or validation)

### Requirement: Keyword URLs not indexed in v1
Keyword/collection URLs SHALL be functional in v1 but SHALL NOT be added to the sitemap. The slug SHALL be stored from v1 to enable a future `/coleccion/{slug}` landing page.

#### Scenario: Sitemap unchanged
- **WHEN** the sitemap is generated after keywords exist
- **THEN** it contains the same product/category URLs as before (no keyword or keyword-filter URLs)

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
