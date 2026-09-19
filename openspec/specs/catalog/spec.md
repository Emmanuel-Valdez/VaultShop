## Purpose

Preserves catalog integrity by making category deletion a guarded operation: a category cannot disappear while active products still reference it, and its calculator-side packaging data is cleaned atomically when deletion succeeds.

## Requirements

### Requirement: Category deletion is blocked when active products exist

The system SHALL prevent soft-deleting a `Category` (`IsDeleted=true`) when at least one `Product` with `IsDeleted=false` still references it via `CategoryId`. The check SHALL count only non-deleted products; soft-deleted or already-removed products SHALL NOT block deletion.

#### Scenario: Delete blocked with count
- **WHEN** an admin calls `POST /{culture}/admin/category/delete/{id}` for a category that has N >= 1 products where `Product.CategoryId == id && !Product.IsDeleted`
- **THEN** the response is `success=false` with a localized message containing N and guidance to reassign products before deleting, the category remains `IsDeleted=false`, and no packaging rows are removed

#### Scenario: Delete allowed when no active products
- **WHEN** an admin deletes a category with zero active products (`Products.Any(CategoryId==id && !IsDeleted) == false`)
- **THEN** the category is soft-deleted (`IsDeleted=true`), its `PackagingByCategory` + `UnitPackagingByCategory` children for that `CategoryId` are hard-deleted in the same `Save()`, and the response is `success=true`

#### Scenario: Deleted products do not block
- **WHEN** a category has only products where `IsDeleted=true` (or zero products) and an admin deletes it
- **THEN** deletion succeeds as above

#### Scenario: Count reflects only active products
- **WHEN** a category has 2 active + 3 soft-deleted products and a delete is attempted
- **THEN** the error message reports `2`, not `5`

### Requirement: Packaging cascade on successful category deletion (option B)

The system SHALL hard-delete the `PackagingByCategory` row (and its `UnitPackagingByCategory` children) whose `CategoryId` equals the deleted category, atomically with the category soft-delete. If no packaging rows exist for that category, deletion SHALL still succeed.

#### Scenario: Category with packaging is deleted
- **WHEN** a category with one `PackagingByCategory` and two `UnitPackagingByCategory` rows is successfully deleted
- **THEN** after the call `PackagingsByCategory` and `UnitsPackagingByCategory` contain zero rows for that `CategoryId`

#### Scenario: Category without packaging is deleted
- **WHEN** a category with no packaging rows is deleted (and has no active products)
- **THEN** deletion succeeds without error

### Requirement: Admin delete feedback shows block reason with count

The admin category list delete flow SHALL surface the server's `success=false` message as a localized error notification (no table reload) and SHALL keep the table unchanged. On `success=true` it SHALL reload the table and show the success toast as today.

#### Scenario: Blocked delete shows error with count
- **WHEN** the server returns `success=false` with message "Category has 3 products..." for a blocked delete
- **THEN** the UI shows that message via `toastr.error` (or equivalent), stays on the same page, and the DataTable is not reloaded

#### Scenario: Successful delete reloads
- **WHEN** the server returns `success=true`
- **THEN** the UI reloads the DataTable and shows `toastr.success`

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
The home page "Shop by Category" shortcuts SHALL navigate by category ID rather than by category name text, so that a category-click never collides with product/keyword text search. Shortcuts SHALL render as the same editorial chip used in Search (circle image or `char.IsLetter` fallback, no count).

#### Scenario: Category shortcut navigates by id
- **WHEN** a shopper clicks a category shortcut on the home page
- **THEN** the app shows products of that category via `categoryId`

#### Scenario: Category chip has no count on home
- **WHEN** category chips are rendered on Home
- **THEN** no count is shown on any chip

### Requirement: Search shows heading hierarchy with minimized filter headings
`Search` SHALL have a single `h1` (Results). The collection hero title SHALL NOT be an `h1` (demoted to `p`/`div` with visual heading style or `aria-labelledby`). Filter-row headings (`Shop by Category` / `Shop by Collection`) SHALL NOT render as `h2` `text-primary` in Search; instead Search filter rows SHALL use either `sr-only` headings or a single muted label `Filtrar por` (`0.75rem uppercase tracking 0.12em`). Home may retain editorial uppercase headings (`Colecciones` / `Categorías` `0.75rem uppercase tracking 0.12em` muted) or equivalent.

#### Scenario: Single h1 in Search with hero
- **WHEN** a shopper views `Search?keywordId=7&slug=naruto` with a cover hero
- **THEN** the page has exactly one `h1` (Results) and the hero title is not an `h1`

#### Scenario: Search filter headings are minimized
- **WHEN** a shopper views Search
- **THEN** no `h2 Shop by Category` / `Shop by Collection` headings are visible; filter rows are labeled by `sr-only` or a single muted `Filtrar por` label

### Requirement: Pagination preserves all storefront filters
Pagination links on storefront results SHALL preserve `keywordId`, `categoryId` and `searchString` across page changes. When a filter is absent, its link parameter SHALL be absent (no empty parameters).

#### Scenario: Filters carried across pages
- **WHEN** a shopper is on page 1 of results with `keywordId=7`, `categoryId=2`, `searchString=negra`
- **THEN** the link to page 2 carries all three values

#### Scenario: Unset filters produce no parameters
- **WHEN** a shopper pages through results with no filters
- **THEN** links contain only the page number and the anchor fragment

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
- **THEN** the hero shows `Title /slug · N productos` and the title carries the brand display style
### Requirement: Pagination preserves slug
Paged navigation on `Search` SHALL carry `slug` together with `keywordId`, `categoryId` and `searchString`.

#### Scenario: Pager keeps slug
- **WHEN** paginated results are shown for `keywordId=7&slug=naruto&categoryId=2&searchString=negra`
- **THEN** the link to page 2 contains all four params

### Requirement: Search shows both filter rows with additive navigation
`Search` SHALL always render the collections row and the categories row (both derived from all visible products, not filtered) using a unified **editorial chip** (72px circle `object-fit:cover`, 6.5rem card, name `clamp 2 lines`). Each chip SHALL preserve the other filters via `AND` navigation (e.g., clicking a collection from `?categoryId=2` goes to `?keywordId=5&categoryId=2`). Active filters SHALL be highlighted (`aria-current`) and offer a `×` that removes only that filter. The chip SHALL NOT display a per-item product count; count is contextual (hero + results header). Filter rows SHALL be horizontal `scroll-row` with `overflow-x:auto + mask fade` and `scroll-snap` on `≤575px` (no wrap).

#### Scenario: Both rows visible on filtered search
- **WHEN** a shopper views `Search?categoryId=2`
- **THEN** both the collections chip row and the categories chip row are rendered as editorial chips

#### Scenario: Collection click preserves category
- **WHEN** a shopper on `Search?categoryId=2` clicks collection `5`
- **THEN** the resulting search is `?keywordId=5&categoryId=2` and shows products matching both

#### Scenario: Category click preserves collection
- **WHEN** a shopper on `Search?keywordId=7` clicks category `2`
- **THEN** the resulting search is `?keywordId=7&categoryId=2` via the same chip component

#### Scenario: Removing one filter keeps the others
- **WHEN** on `Search?keywordId=7&categoryId=2&searchString=negra` the shopper removes the collection filter
- **THEN** the resulting URL is `?categoryId=2&searchString=negra`

#### Scenario: Chip has no count, hero/header carry count
- **WHEN** a chip for a collection or category is rendered
- **THEN** its visual label shows only the name (and `title="N productos"` for hover is optional) and neither `.collection-chip__count` nor a count badge is rendered; the active collection count is visible only in `CollectionHero` (`Title /slug · N productos`) and in the results header

#### Scenario: Filter rows scroll horizontally on mobile
- **WHEN** viewport is `≤575px` and filter chips overflow
- **THEN** rows scroll horizontally with `mask fade` and no wrapping, and each chip and its `×` meet `≥44px` touch target with `focus-visible` accent ring
### Requirement: Product detail shows its collections
`Details` SHALL list the product's assigned keywords as linkable collection chips below the category badge, each linking to `Search?keywordId`. When a product has no keywords, no chip row SHALL be rendered.

#### Scenario: Product with collections shows chips
- **WHEN** a product belongs to keywords `7` and `9`
- **THEN** its detail page shows two collection chips linking to `Search?keywordId=7` and `Search?keywordId=9`

#### Scenario: Product without collections shows no row
- **WHEN** a product belongs to no keywords
- **THEN** the detail page renders no collection chip row
