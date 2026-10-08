## MODIFIED Requirements

### Requirement: Unified storefront price visibility

Every storefront product listing and detail view (Home, featured products, Search, Favorites, and Product Details) SHALL display a single authoritative unit price resolved by a shared rule in this order: (1) base price — wholesale price when the viewer is a company user or an admin/employee in wholesale preview mode, otherwise retail price; (2) the single best eligible specific discount (direct offer, BxGy share, collection offer — best-price-wins, wholesale excluded by default); (3) informational display of any applicable payment-method discount when that method is selected. The card, search result, favorites row, and details price block SHALL all apply the same rule, and discounted prices SHALL show the original price struck-through with a localized badge naming the motive.

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

#### Scenario: Active offer shows strikethrough and badge
- **WHEN** a retail viewer loads any listing or details for a product with an active direct offer
- **THEN** the offer price is shown with the original price struck-through and a localized offer badge

### Requirement: Cart and checkout use the same unified price

Cart totals and order creation SHALL price each line item using the same unified rule as storefront display: (1) base role price (wholesale when the actor is a company user or an admin/employee in wholesale preview, otherwise retail); (2) the single best eligible specific discount per line/subtotal (best-price-wins, never stacked); (3) the payment-method discount applied to the eligible subtotal after line discounts. The cart summary and checkout summary SHALL reflect that resolved price with visible discount lines and motives, and the coupon SHALL be re-validated at order creation.

#### Scenario: Company checkout uses wholesale
- **WHEN** a Company user builds a cart summary and creates an order
- **THEN** each cart line and the order total are calculated from the wholesale price

#### Scenario: Admin in wholesale preview checkout uses wholesale
- **WHEN** an admin or employee in wholesale preview builds a cart summary and creates an order
- **THEN** each cart line and the order total are calculated from the wholesale price

#### Scenario: Retail checkout uses retail
- **WHEN** a retail customer or an admin in retail preview builds a cart summary and creates an order
- **THEN** each cart line and the order total are calculated from the retail price

#### Scenario: Discounted checkout shows motive lines
- **WHEN** a retail cart qualifies for a BxGy benefit and the transfer payment discount
- **THEN** the summary shows the BxGy line discount with its motive plus the payment-method discount line, and the order freezes both
