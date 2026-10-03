## Purpose

Gives every product and category a stable, human-readable canonical URL (id plus slug) so storefront links, shares, and search indexes point at self-describing addresses while ids remain the lookup truth.

## Requirements

### Requirement: Products have URL slugs

Every non-deleted product SHALL have a URL slug derived from its name when the admin leaves the slug blank, or from the admin-supplied slug otherwise. Slugs SHALL be normalized (lowercase ASCII, non-alphanumeric runs collapsed to a single hyphen, no leading/trailing hyphens) and SHALL be unique among non-deleted products; collisions SHALL resolve with a numeric suffix (`-2`, `-3`). An empty result after normalization SHALL be rejected with a localized validation error. Soft-deleted products SHALL NOT block slug reuse.

#### Scenario: Blank slug auto-generates from name
- **WHEN** an admin saves a product named "Remera Negra" with a blank slug
- **THEN** the product slug is `remera-negra`

#### Scenario: Manual slug is normalized
- **WHEN** an admin saves slug "Mi REMERA!! negra"
- **THEN** the stored slug is `mi-remera-negra`

#### Scenario: Duplicate slug is rejected
- **WHEN** an admin saves a product with slug `remera-negra` while another non-deleted product already uses it
- **THEN** the save is rejected with a localized "slug already exists" error

#### Scenario: Deleted product frees its slug
- **WHEN** the only product using `remera-negra` is soft-deleted
- **THEN** a new product MAY claim `remera-negra`

### Requirement: Categories have URL slugs with full detail URLs

Every non-deleted category SHALL have a URL slug with the same generation, normalization, uniqueness, and reuse rules as products (scoped to categories). Each category SHALL have a canonical detail URL containing its id and slug.

#### Scenario: Category slug auto-generates
- **WHEN** an admin saves a category named "Camperas de Invierno" with a blank slug
- **THEN** the category slug is `camperas-de-invierno`

#### Scenario: Category detail URL is canonical
- **WHEN** a shopper visits the category detail URL with the correct slug
- **THEN** the page renders with HTTP 200 and its canonical link matches the visited URL

### Requirement: Product detail URL is id plus slug with canonical redirect

The product detail page SHALL be addressable as `/Details/{id}/{slug}`. Lookup SHALL be by id only; the slug SHALL NOT affect which product renders. A mismatched non-empty slug SHALL produce a permanent redirect (301) to the canonical slug URL preserving culture. A missing slug SHALL render normally with HTTP 200 and no redirect; rendered links and pager targets SHALL include the slug so the next navigation is canonical.

#### Scenario: Correct slug renders
- **WHEN** a shopper visits `/Details/123/remera-negra` for product 123 whose slug is `remera-negra`
- **THEN** the detail page renders with HTTP 200

#### Scenario: Wrong slug redirects permanently
- **WHEN** a shopper visits `/Details/123/wrong-slug`
- **THEN** the system responds with a 301 redirect to `/Details/123/remera-negra` preserving culture

#### Scenario: Missing slug renders without redirect
- **WHEN** a shopper visits the detail URL for product 123 with no slug
- **THEN** the page renders with HTTP 200 and no redirect occurs

#### Scenario: Unknown id is 404
- **WHEN** a shopper visits `/Details/99999/anything` for a soft-deleted or non-existent product
- **THEN** the system returns 404

### Requirement: Existing products and categories are backfilled

The migration SHALL assign slugs to all existing non-deleted products and categories that lack one, derived from their names with the same normalization and collision-suffix rules. Slugs already set SHALL be left untouched.

#### Scenario: Backfill generates unique slugs
- **WHEN** two existing products are both named "Remera Negra" with no slugs
- **THEN** after migration one has `remera-negra` and the other `remera-negra-2`

### Requirement: Sitemap and canonical tags use slug URLs

The sitemap SHALL list product detail URLs and category detail URLs in their canonical id-plus-slug form. Product and category pages SHALL emit a `<link rel="canonical">` matching the sitemap URL.

#### Scenario: Sitemap lists canonical product URL
- **WHEN** a crawler fetches `/sitemap.xml`
- **THEN** each available product appears as its canonical `/Details/{id}/{slug}` URL, never the id-only form
