## MODIFIED Requirements

### Requirement: Unified storefront price visibility

Every storefront product listing and detail view (Home, featured products, Search, Favorites, and Product Details) SHALL display a single authoritative unit price resolved by a shared rule in this order: (1) base price — wholesale price when the viewer is a company user or an admin/employee in wholesale preview mode, otherwise retail price; (2) the single best eligible specific discount that can apply to a single unit (direct offer, collection offer — best-price-wins, wholesale excluded by default); (3) informational display of any applicable payment-method discount when that method is selected. The displayed unit price SHALL NOT include a BxGy grant, which requires at least `BuyQty + GetQty` units present in the cart; before the cart qualifies, BxGy is surfaced as a promotion badge without changing the unit price. The card, search result, favorites row, and details price block SHALL all apply the same rule, and discounted prices SHALL show the original price struck-through with a localized badge naming the motive.

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

#### Scenario: BxGy does not reduce a single-unit listing price
- **WHEN** a retail viewer loads Home, Search, or Favorites where a mixed BxGy promotion covers one or more listed products
- **THEN** each product shows its full unit price (direct offer and collection offer aside) with the promotion badge, and no struck-through combo price is rendered