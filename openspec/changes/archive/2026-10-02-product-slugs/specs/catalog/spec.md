## ADDED Requirements

### Requirement: Storefront product links carry slugs

All storefront product links (home, search results, cart, favorites) SHALL render the canonical id-plus-slug form. Pagination on filtered results SHALL preserve product and category slugs alongside the existing `keywordId`, `categoryId`, `searchString`, and collection `slug` parameters; absent filters SHALL produce absent parameters.

#### Scenario: Product card links carry slug
- **WHEN** a product with `Id=123, Slug=remera-negra` renders on home or search
- **THEN** its image and title links point at the canonical `/Details/123/remera-negra` form

#### Scenario: Pager keeps all slugs
- **WHEN** paged results are shown with `keywordId=7&slug=naruto&categoryId=2&searchString=negra`
- **THEN** the link to page 2 carries all of those parameters
