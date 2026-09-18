## ADDED Requirements

### Requirement: Search preserves slug alongside filters
When a collection filter is active, the system SHALL expose the keyword `Slug` in the search URL as an optional decorative `slug` param alongside `keywordId`. Filtering SHALL remain by `keywordId` only; `slug` SHALL NOT affect result set. `keywordId` + `slug` mismatch SHALL redirect to the canonical `slug`.

#### Scenario: Collection search link carries slug
- **WHEN** the storefront renders a collection chip for keyword `Id=7, Slug=naruto`
- **THEN** its link includes both `keywordId=7` and `slug=naruto` while preserving `categoryId` and `searchString` if present

#### Scenario: Slug mismatch redirects to canonical
- **WHEN** a shopper visits `Search?keywordId=7&slug=wrong`
- **THEN** the system responds with a redirect to `Search?keywordId=7&slug=naruto` (canonical) preserving other params

#### Scenario: Slug absent yields clean url without redirect
- **WHEN** a shopper visits `Search?keywordId=7` without `slug`
- **THEN** results are the same as with `slug` and no redirect occurs (pager adds `slug` on next navigation)

#### Scenario: Slug visible in active collection context
- **WHEN** a shopper is on `Search?keywordId=7&slug=naruto`
- **THEN** the active collection context shows `/{slug}` alongside the name (e.g., breadcrumb or heading)

### Requirement: Pagination preserves slug
Paged navigation on `Search` SHALL carry `slug` together with `keywordId`, `categoryId` and `searchString`.

#### Scenario: Pager keeps slug
- **WHEN** paginated results are shown for `keywordId=7&slug=naruto&categoryId=2&searchString=negra`
- **THEN** the link to page 2 contains all four params

### Requirement: Search shows both filter rows with additive navigation
`Search` SHALL always render the collections row and the categories row (both derived from all visible products, not filtered). Each chip/button SHALL preserve the other filters via `AND` navigation (e.g., clicking a collection from `?categoryId=2` goes to `?keywordId=5&categoryId=2`). Active filters SHALL be highlighted (`aria-current`) and offer a `×` that removes only that filter.

#### Scenario: Both rows visible on filtered search
- **WHEN** a shopper views `Search?categoryId=2`
- **THEN** both the collections chip row and the categories pill row are rendered

#### Scenario: Collection click preserves category
- **WHEN** a shopper on `Search?categoryId=2` clicks collection `5`
- **THEN** the resulting search is `?keywordId=5&categoryId=2` and shows products matching both

#### Scenario: Category click preserves collection
- **WHEN** a shopper on `Search?keywordId=7` clicks category `2`
- **THEN** the resulting search is `?keywordId=7&categoryId=2`

#### Scenario: Removing one filter keeps the others
- **WHEN** on `Search?keywordId=7&categoryId=2&searchString=negra` the shopper removes the collection filter
- **THEN** the resulting URL is `?categoryId=2&searchString=negra`

### Requirement: Product detail shows its collections
`Details` SHALL list the product's assigned keywords as linkable collection chips below the category badge, each linking to `Search?keywordId`. When a product has no keywords, no chip row SHALL be rendered.

#### Scenario: Product with collections shows chips
- **WHEN** a product belongs to keywords `7` and `9`
- **THEN** its detail page shows two collection chips linking to `Search?keywordId=7` and `Search?keywordId=9`

#### Scenario: Product without collections shows no row
- **WHEN** a product belongs to no keywords
- **THEN** the detail page renders no collection chip row

