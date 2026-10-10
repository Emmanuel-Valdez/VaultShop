## MODIFIED Requirements

### Requirement: Product detail requires explicit variant selection

When a product has purchasable variant combinations, its detail page SHALL render one selector per option type used by the product, and adding to cart SHALL require an explicitly selected value for every type. No default variant SHALL be preselected and the base product without a variant SHALL NOT be purchasable. Products without variants SHALL render and behave exactly as today, and no variant SHALL get its own URL (links, slugs, sitemap, and SEO behavior unchanged). The unavailable-combination message SHALL stay hidden until every selector has a value and that combination is missing or unavailable. An option value that cannot form an available combination with the current selection SHALL be disabled. Favoriting SHALL NOT require a variant selection.

#### Scenario: Selectors render per option type

- **WHEN** a shopper views the backpack (types `Casa`, `Tamaño`)
- **THEN** the page shows a `Casa` selector with 4 options and a `Tamaño` selector with 2 options

#### Scenario: Add without selection is rejected

- **WHEN** a shopper posts add-to-cart for a product with variants without selecting all types
- **THEN** the request is rejected with a localized message and nothing is added to the cart

#### Scenario: Add with an invalid or disabled variant is rejected

- **WHEN** a shopper posts a variant id that belongs to another product, covers a wrong set of types, or is disabled
- **THEN** the request is rejected and nothing is added to the cart

#### Scenario: Valid selection resolves to one combination

- **WHEN** a shopper selects `Casa=Gryffindor` + `Tamaño=15"` and adds 2 units (within stock)
- **THEN** the cart gains (or merges) the `Gryffindor / 15"` line with count 2

#### Scenario: Variant-less detail page unchanged

- **WHEN** a shopper views and buys a product with no variants
- **THEN** no selectors render and add-to-cart works as before this change

#### Scenario: Incomplete selection shows no unavailable message

- **WHEN** a shopper opens a variant product, or returns to it after adding a valid combination to the cart, before choosing every option
- **THEN** the unavailable-combination message is hidden

#### Scenario: Complete unavailable selection shows the message

- **WHEN** a shopper selects a value for every option type and that combination does not exist or is unavailable
- **THEN** the unavailable-combination message is shown and add-to-cart stays disabled

#### Scenario: Impossible option is disabled

- **WHEN** a shopper selects a value that leaves another option type with no available combination
- **THEN** those impossible values are disabled before the shopper submits

#### Scenario: Favorite ignores variant selection

- **WHEN** a signed-in shopper favorites a variant product without selecting any option
- **THEN** the product is saved as a favorite and no variant is required

## ADDED Requirements

### Requirement: Products with no available variant are not sold

A product that has variant combinations and none of them are available SHALL NOT appear in storefront listings and SHALL NOT be purchasable. Favoriting that product SHALL remain allowed when the product itself is still available in the store.

#### Scenario: Unsellable variant product is omitted from home and search

- **WHEN** a product is available in the store but every variant combination is unavailable
- **THEN** home and search do not list it

#### Scenario: Direct detail cannot add it to the cart

- **WHEN** a shopper opens the detail page of that product
- **THEN** add-to-cart is not offered

### Requirement: Cart and favorites omit the description

The cart page and the favorites page SHALL NOT render the product description. The product detail page SHALL still render it.

#### Scenario: Long description stays off the cart

- **WHEN** a product with a long description is in the cart or in favorites
- **THEN** those pages show name, price, and variant label without the description body
