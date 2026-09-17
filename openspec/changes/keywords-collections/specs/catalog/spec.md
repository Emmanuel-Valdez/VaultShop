## ADDED Requirements

### Requirement: Search supports exact keyword and category filters
The storefront search action SHALL accept a `keywordId` and a `categoryId` filter. When `keywordId` is set, results SHALL include only products associated with that exact keyword (by ID, never by text matching). When `categoryId` is set, results SHALL include only products of that category by ID. When both are set, both SHALL apply (AND). The existing text `searchString` matching semantics (accent/case-insensitive `CompareInfo.IndexOf` over name, category name, description) SHALL remain the same and SHALL combine with the filters via AND.

#### Scenario: Keyword id filter narrows results
- **WHEN** `searchString` is empty and `keywordId=7` is passed
- **THEN** results are exactly the products associated with keyword id 7

#### Scenario: Category id filter narrows results
- **WHEN** `searchString` is empty and `categoryId=2` is passed
- **THEN** results are exactly the products of category id 2

#### Scenario: Keyword, category and text combine with AND
- **WHEN** `keywordId=7`, `categoryId=2` and `searchString=negra` are all passed
- **THEN** results are the products that belong to category 2, are associated with keyword 7, and match the text search "negra"

#### Scenario: Invalid filter ids yield empty results
- **WHEN** `keywordId` or `categoryId` references a soft-deleted or non-existent record
- **THEN** the search returns no results (no exception, no unrelated products)

### Requirement: Home category shortcuts use category id
The home page "Shop by Category" shortcuts SHALL navigate by category ID rather than by category name text, so that a category-click never collides with product/keyword text search.

#### Scenario: Category shortcut navigates by id
- **WHEN** a shopper clicks a category shortcut on the home page
- **THEN** the app shows products of that category via `categoryId`

### Requirement: Pagination preserves all storefront filters
Pagination links on storefront results SHALL preserve `keywordId`, `categoryId` and `searchString` across page changes. When a filter is absent, its link parameter SHALL be absent (no empty parameters).

#### Scenario: Filters carried across pages
- **WHEN** a shopper is on page 1 of results with `keywordId=7`, `categoryId=2`, `searchString=negra`
- **THEN** the link to page 2 carries all three values

#### Scenario: Unset filters produce no parameters
- **WHEN** a shopper pages through results with no filters
- **THEN** links contain only the page number and the anchor fragment