## ADDED Requirements

### Requirement: Shipped orders are frozen in the application

Once an order reaches `OrderStatus=Shipped`, the system SHALL reject every mutation of that order through the application — detail updates, branch correction, cancel, and refund — for all roles; shipped orders render fully read-only.

#### Scenario: Admin cannot edit shipped order details

- **WHEN** an admin posts detail changes (address, carrier, tracking, branch) to a shipped order
- **THEN** the server rejects the request, persists nothing, and redirects to the order details view

#### Scenario: Cancel or refund of shipped order rejected

- **WHEN** a cancel or refund request targets a shipped order
- **THEN** the server rejects it and the order is unchanged

#### Scenario: Shipped order shows no mutating actions

- **WHEN** any user views a shipped order
- **THEN** no update, ship, cancel, or pay-now action is offered and all inputs are read-only

### Requirement: Pickup branch correction window

For `DeliveryType=S` orders that are neither shipped nor terminal, the system SHALL allow an admin to replace the pickup branch snapshot (code, name, address, hours) from the branch table; the correction SHALL be logged and SHALL NOT send a notification email.

#### Scenario: Correction before shipment

- **WHEN** an admin corrects the branch on an unshipped, non-terminal pickup order
- **THEN** the snapshot is overwritten from the branch table and the correction is logged

#### Scenario: No correction email on branch change

- **WHEN** an admin corrects the branch
- **THEN** no email is sent at correction time; the shipping email later carries the final branch
