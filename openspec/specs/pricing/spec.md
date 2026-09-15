# pricing Specification

## Purpose
Sets a per-product monthly production expectation as the basis for fixed-cost allocation in price suggestions, so each product absorbs a fixed-cost share proportional to its own expected volume instead of sharing a category-level figure.

## Requirements

### Requirement: Fixed-cost allocation uses the product's own expectation

The system SHALL distribute total monthly fixed costs across products by dividing the fixed cost total by each product's monthly expectation (`product.MaxExpectation`). The category-level expectation SHALL NOT influence the per-unit fixed-cost share.

#### Scenario: Products in the same category with different expectations
- **WHEN** two active products share a category whose expectation is 10, but product A has `MaxExpectation = 10` and product B has `MaxExpectation = 20`, and total fixed costs are 1000
- **THEN** product A's fixed-cost share is 100 (1000 / 10) and product B's is 50 (1000 / 20)

#### Scenario: Category expectation no longer affects cost
- **WHEN** the category's stored expectation changes (if still present during transition) or is removed
- **THEN** the per-unit fixed-cost share reported for every product in that category is unchanged unless a product's own expectation changes

### Requirement: Product carries a required monthly expectation

Every `Product` SHALL have a required `MaxExpectation` (monthly production expectation) accepted only when within the inclusive range 1–10000. Products whose expectation is missing or out of range SHALL NOT be saved by admin upsert.

#### Scenario: Valid expectation saved
- **WHEN** an admin creates or edits a product with `MaxExpectation` between 1 and 10000
- **THEN** the product is saved with that expectation and it is used by the cost calculation

#### Scenario: Expectation below range rejected
- **WHEN** an admin submits a product upsert with `MaxExpectation = 0` or negative
- **THEN** the upsert shows a localized validation error and the product is not saved

#### Scenario: Expectation above range rejected
- **WHEN** an admin submits a product upsert with `MaxExpectation > 10000`
- **THEN** the upsert shows a localized validation error and the product is not saved

#### Scenario: Expectation missing rejected
- **WHEN** an admin submits a product upsert without a `MaxExpectation` value
- **THEN** the upsert shows a localized validation error and the product is not saved

### Requirement: Existing expectations are preserved through migration

The migration SHALL transfer each product's expectation from its category so no product is left without one: every product present at migration time receives its category's `MaxExpectation`. After migration the category SHALL no longer carry an expectation field.

#### Scenario: Product inherits its category's expectation
- **WHEN** migration runs and a product's category had `MaxExpectation = 25`
- **THEN** the product's `MaxExpectation` is 25 after migration

#### Scenario: Category field removed
- **WHEN** migration completes
- **THEN** the `Category` model and its database table no longer expose a `MaxExpectation` column

### Requirement: New products default to a suggested expectation

The admin product creation form SHALL pre-fill the expectation with the value 30 as a suggested starting amount; the admin SHALL be able to change it before saving.

#### Scenario: New product upsert prefilled
- **WHEN** an admin opens the upsert form for a new product
- **THEN** the monthly expectation input shows 30 and accepts edits

### Requirement: Cost report reflects the per-product expectation

The cost-by-product report SHALL show each product's own expectation as the value reported as monthly expectation, together with the resulting per-unit fixed-cost share.

#### Scenario: Report row shows product expectation
- **WHEN** the admin views the cost-by-product report
- **THEN** the "max expectation monthly" column of each row equals that product's `MaxExpectation` and the fixed-cost-added column equals `totalFixedCost / MaxExpectation` for that product

### Requirement: Retail price below wholesale is allowed but warned

The admin product upsert SHALL NOT block saving when the final retail price is lower than the final wholesale price; the system SHALL show a non-blocking SweetAlert2 warning informing the admin, and the save SHALL proceed unchanged.

#### Scenario: Retail below wholesale warned but saved
- **WHEN** an admin submits a product upsert where `FinalRetailPrice < FinalWholesalePrice`
- **THEN** a warning is shown and the product is saved with the entered retail price

#### Scenario: Retail equal or above wholesale
- **WHEN** an admin submits a product upsert where `FinalRetailPrice >= FinalWholesalePrice`
- **THEN** no warning is shown and the product is saved with the entered retail price

### Requirement: Calculated price label in Spanish

The calculated-price field on the product creation/editing form SHALL be labeled "Precio Calculado Sugerido" when the UI is in Spanish (es-AR). The English label SHALL remain unchanged.

#### Scenario: Spanish form label
- **WHEN** an admin opens the product upsert with the UI in Spanish
- **THEN** the calculated-price field label reads "Precio Calculado Sugerido"

#### Scenario: English form label unchanged
- **WHEN** an admin opens the product upsert with the UI in English
- **THEN** the calculated-price field label is unchanged from its current value

### Requirement: Help section consistent with the pricing specs

The help section (product creation, costs, final prices) SHALL document the new pricing behavior: the monthly expectation is set per product (pre-filled with 30 on new products) and is no longer entered at category level, and a retail price below the wholesale price triggers a non-blocking warning.

#### Scenario: Product creation help
- **WHEN** an admin reads the help section for product creation
- **THEN** it explains that the monthly expectation is a per-product field, pre-filled with 30 for new products

#### Scenario: Fixed-cost help
- **WHEN** an admin reads the help section for fixed-cost allocation
- **THEN** it refers to the per-product expectation rather than a per-category figure

#### Scenario: Retail/wholesale warning documented
- **WHEN** an admin reads the help section for final prices
- **THEN** it mentions that a retail price below the wholesale price is allowed but shows a warning

### Requirement: Unified storefront price visibility

Every storefront product listing and detail view (Home, featured products, Search, Favorites, and Product Details) SHALL display a single authoritative unit price resolved by a shared rule: wholesale price when the viewer is a company user or an admin/employee in wholesale preview mode, otherwise retail price. The card, search result, favorites row, and details price block SHALL all apply the same rule.

#### Scenario: Company user sees wholesale on Home
- **WHEN** a user in the Company role loads Home (including featured products)
- **THEN** each product card shows the wholesale price

#### Scenario: Company user sees wholesale on Search and Favorites
- **WHEN** a user in the Company role loads Search results or Favorites
- **THEN** each product entry shows the wholesale price

#### Scenario: Retail customer sees retail everywhere
- **WHEN** a user not in Company and not in wholesale preview loads Home, Search, Favorites, or Details
- **THEN** each product shows the retail price

#### Scenario: Admin in wholesale preview sees wholesale everywhere
- **WHEN** an admin or employee with wholesale preview active loads Home, Search, Favorites, or Details
- **THEN** each product shows the wholesale price

#### Scenario: Admin in retail preview sees retail
- **WHEN** an admin or employee with retail preview (or no wholesale preview) loads any storefront listing or details
- **THEN** each product shows the retail price

#### Scenario: Details strikethrough consistent with listing
- **WHEN** a product is shown in Details to a viewer who sees wholesale (Company or admin in wholesale preview)
- **THEN** the retail price is shown struck-through above the wholesale price; otherwise only the retail price is shown

### Requirement: Cart and checkout use the same unified price

Cart totals and order creation SHALL price each line item using the same unified rule as storefront display: wholesale price when the actor is a company user or an admin/employee in wholesale preview, otherwise retail price. The cart summary and checkout summary SHALL reflect that resolved price.

#### Scenario: Company checkout uses wholesale
- **WHEN** a Company user builds a cart summary and creates an order
- **THEN** each cart line and the order total are calculated from the wholesale price

#### Scenario: Admin in wholesale preview checkout uses wholesale
- **WHEN** an admin or employee in wholesale preview builds a cart summary and creates an order
- **THEN** each cart line and the order total are calculated from the wholesale price

#### Scenario: Retail checkout uses retail
- **WHEN** a retail customer or an admin in retail preview builds a cart summary and creates an order
- **THEN** each cart line and the order total are calculated from the retail price
