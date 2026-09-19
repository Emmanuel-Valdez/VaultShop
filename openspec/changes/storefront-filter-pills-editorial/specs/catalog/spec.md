## MODIFIED Requirements

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
