## Purpose

Allows admins and employees to preview the storefront as a retail or wholesale customer to verify pricing visibility without separate accounts.

## Requirements

### Requirement: Admin wholesale preview toggle

Admins and employees SHALL be able to switch the storefront preview between retail and wholesale modes from the navigation header. The system SHALL persist the selected mode in the HTTP session and SHALL scope it so only admins and employees can set or affect it; customers and company users SHALL NOT be able to set or inherit preview state.

#### Scenario: Admin switches to wholesale preview
- **WHEN** an admin or employee selects wholesale preview from the header control
- **THEN** the session stores wholesale preview mode and subsequent storefront requests for that session render wholesale pricing where applicable

#### Scenario: Admin switches back to retail preview
- **WHEN** an admin or employee who is in wholesale preview selects retail preview
- **THEN** the session stores retail preview mode and subsequent storefront requests render retail pricing

#### Scenario: Non-admin cannot affect preview
- **WHEN** a customer or company user attempts to set preview mode directly
- **THEN** the request is ignored or rejected and storefront pricing remains determined solely by their role

#### Scenario: Preview resets on session end
- **WHEN** the session expires or is cleared
- **THEN** preview mode returns to the default retail state

### Requirement: Preview mode indicator banner

When an admin or employee has wholesale preview active, the storefront SHALL display a prominent dismissible banner indicating wholesale preview is active and offering an action to return to retail preview. The banner SHALL NOT appear for customers, company users, or admins in retail preview.

#### Scenario: Banner visible in wholesale preview
- **WHEN** an admin with wholesale preview loads any storefront page
- **THEN** a banner is visible stating wholesale preview is active with a control to exit it

#### Scenario: Banner hidden in retail preview
- **WHEN** an admin with retail preview or no preview loads a storefront page
- **THEN** no preview banner is shown

#### Scenario: Banner is accessible
- **WHEN** the banner is rendered
- **THEN** it uses an accessible role and label, and its exit control is keyboard-operable

### Requirement: Preview does not affect order pricing authority

Preview mode SHALL affect only the rendered price and cart/checkout price resolution for the admin/employee session; it SHALL NOT grant wholesale checkout to non-company customers and SHALL NOT alter persisted order pricing rules beyond the session's price resolution.

#### Scenario: Admin in wholesale preview adds to cart
- **WHEN** an admin in wholesale preview adds a product to cart and proceeds to checkout
- **THEN** cart and checkout totals are calculated using the wholesale price for that session

#### Scenario: Preview does not persist to other users
- **WHEN** another user loads the storefront concurrently
- **THEN** their pricing is unaffected by the first user's preview state
