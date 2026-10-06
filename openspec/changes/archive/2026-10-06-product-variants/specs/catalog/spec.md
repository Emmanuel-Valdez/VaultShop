## ADDED Requirements

### Requirement: Product detail requires explicit variant selection

When a product has purchasable variant combinations, its detail page SHALL render one selector per option type used by the product, and adding to cart SHALL require an explicitly selected value for every type. No default variant SHALL be preselected and the base product without a variant SHALL NOT be purchasable. Products without variants SHALL render and behave exactly as today, and no variant SHALL get its own URL (links, slugs, sitemap, and SEO behavior unchanged).

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
