## Purpose

Lets admins attach a single representative thumbnail to each category and renders it in the storefront category filters.

## ADDED Requirements

### Requirement: Category stores one image

A `Category` SHALL carry at most one image, described by its display URL, storage object key, original file name, content type, size in bytes, and storage provider. Existing categories without an image SHALL remain valid and unimaged.

#### Scenario: New category starts without an image
- **WHEN** an admin creates a category without uploading an image
- **THEN** the category is saved with no image and the storefront shows its fallback avatar

#### Scenario: Category image is persisted with storage metadata
- **WHEN** an admin uploads an image for a category
- **THEN** the category stores the image display URL, object key, file name, content type, size, and provider

### Requirement: Admin upload validates and normalizes the category image

The admin category upsert SHALL accept an image upload and reject it when the file is empty, larger than 10 MB, has a non-image extension (jpg/jpeg/png/webp allowed), has a non-image content type (image/jpeg, image/png, image/webp allowed), or cannot be decoded as an image. Accepted images SHALL be decoded with orientation applied, center-square-cropped, resized to ~400px, re-encoded as JPEG (quality 75), and stored under the object path `categories/category-{id}`.

#### Scenario: Invalid upload is rejected with a localized message
- **WHEN** an admin submits an unsupported file type, empty file, oversized file, or undecodable image
- **THEN** the upsert fails with a localized error, the category is not modified, and no storage object is created

#### Scenario: Valid upload is normalized and stored
- **WHEN** an admin uploads a valid photo (e.g., a landscape PNG)
- **THEN** the stored image is a JPEG of at most ~400px, center-cropped to a square, and referenced from `categories/category-{id}`

### Requirement: Admin replace removes the previous image

When an upload replaces an existing category image, the new image SHALL be saved and persisted first, and the previous storage object SHALL be deleted only after the database is consistent. A failure to delete the old object SHALL NOT fail the upsert.

#### Scenario: Replacing an image swaps storage objects
- **WHEN** an admin uploads a new image for a category that already has one
- **THEN** the category references only the new object, and the previous object is deleted from storage

#### Scenario: Before/after ordering prevents data loss on failure
- **WHEN** an admin replaces an image and saving the new object fails
- **THEN** the category keeps its previous image and its storage object is untouched

### Requirement: Admin can delete the category image

The admin category upsert SHALL expose a delete-image action. Deleting SHALL remove the image metadata row/columns and, after the database is consistent, best-effort delete the storage object.

#### Scenario: Deleting an image clears it and the fallback returns
- **WHEN** an admin deletes the image of an imaged category
- **THEN** the category no longer references an image, the storage object is deleted, and the storefront shows the fallback avatar

### Requirement: Category soft-delete removes its image storage

When a category is soft-deleted, its image storage object SHALL be deleted (best-effort) as part of the deletion flow, mirroring keyword behavior.

#### Scenario: Soft-deleted category cleans up its image
- **WHEN** an admin deletes a category that has an image and no active products
- **THEN** the category is soft-deleted (`IsDeleted=true`) and its image object is removed from storage

### Requirement: Storefront category filters show thumbnails with fallback

The "Shop by Category" filter rows on Home and Search SHALL render each category as a compact pill with a small rounded thumbnail before its label. A category without an image SHALL show a first-letter avatar (first character of the name, uppercased) instead. The thumbnail SHALL be hidden from assistive technology (`aria-hidden`) with the category name as the accessible label.

#### Scenario: Imaged category renders thumbnail in its pill
- **WHEN** the home or search page renders a category that has an image
- **THEN** its pill shows the category thumbnail followed by the name, and the pill still navigates by `categoryId`

#### Scenario: Unimaged category renders a letter avatar
- **WHEN** the home or search page renders a category without an image
- **THEN** its pill shows an uppercased first-letter avatar instead of a thumbnail

#### Scenario: Filter behavior is unchanged
- **WHEN** a shopper clicks a category pill with a thumbnail
- **THEN** the search navigates by `categoryId` exactly as before, preserving the other active filters