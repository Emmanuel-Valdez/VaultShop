## ADDED Requirements

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
