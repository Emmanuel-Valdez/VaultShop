## ADDED Requirements

### Requirement: Customer order history uses the shared read-only order list

Authenticated customers and company users SHALL reach their order history through the existing shared order list and order Details pages, filtered server-side to the orders they are allowed to access. Anonymous requests SHALL be redirected to sign in. Non-staff users SHALL NOT be offered controls that edit order or shipping data; existing customer payment actions remain available. The system SHALL NOT require a separate customer order route.

#### Scenario: Customer sees only their own orders
- **WHEN** a customer opens the order history
- **THEN** the list contains only that customer's non-company orders

#### Scenario: Company user sees company orders
- **WHEN** a company user opens the order history
- **THEN** the list contains the current company's orders plus that user's personal non-company orders

#### Scenario: Read-only details
- **WHEN** a non-staff user opens an order they may access
- **THEN** order and shipping data are presented read-only, and only the existing payment actions are available

#### Scenario: Anonymous access
- **WHEN** an anonymous visitor requests the order history
- **THEN** they are redirected to sign in

#### Scenario: Cross-user access is denied
- **WHEN** a customer requests the Details page for another customer's order
- **THEN** the order is not disclosed
