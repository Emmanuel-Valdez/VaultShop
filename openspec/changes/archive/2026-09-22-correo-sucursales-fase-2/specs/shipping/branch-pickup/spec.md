## Purpose

Defines deterministic Correo Argentino branch pickup: branch hours data from seed to order snapshot, cascade branch selection replacing geocoded search, pre-shipment admin correction, and the frozen-after-shipped edit rule.

## ADDED Requirements

### Requirement: Branch hours flow from seed data to order snapshot

The system SHALL persist branch hours from the seed file onto the branch table and snapshot them onto pickup orders at creation, so every surface (checkout candidates, order details, summary, emails) renders the hours the buyer saw.

#### Scenario: Hours seeded from JSON

- **WHEN** the application seeds branch data from `sucursales.json`
- **THEN** each branch row carries its `horario` value as hours (fallback sentinel `no informa` only when the source row has none)

#### Scenario: Order snapshots hours at creation

- **WHEN** a `DeliveryType=S` order is created with a valid branch code
- **THEN** the order stores code, name, address, and hours as of purchase time; later branch-table changes never rewrite history

#### Scenario: Forged branch code rejected

- **WHEN** a checkout or admin POST carries a branch code that is not an eligible pickup branch
- **THEN** the submission is rejected with a validation error and no order is created or modified

### Requirement: Cascade branch selection replaces nearest search

The system SHALL offer exactly one branch-selection mode in checkout: `Provincia` select → `Localidad` select (scoped within the province) → branch radio list showing name, address, and hours; the geocoded nearest-5 search SHALL NOT exist.

#### Scenario: Full cascade to selection

- **WHEN** a buyer picks a province, then a locality, then a branch radio
- **THEN** the order can be placed with that branch; the submit control stays disabled until a branch is selected (when scripting is available)

#### Scenario: Failed POST preserves cascade state

- **WHEN** a checkout POST fails validation
- **THEN** the re-rendered form keeps the typed address, the selected province/locality, the branch candidates, and the previously picked branch

#### Scenario: Locality scoped by province

- **WHEN** two provinces contain a locality with the same name
- **THEN** each province shows only its own branches; a branch lookup without province context is never used for listing

### Requirement: Admin branch correction before shipment

An admin SHALL be able to change the pickup branch of an order that is not shipped and not terminal, using the same cascade selector; the order snapshot (code, name, address, hours) is overwritten from the branch table. No notification email is sent on correction; the shipping email carries the final branch.

#### Scenario: Pre-shipment correction succeeds

- **WHEN** an admin changes the branch on an unshipped, non-terminal pickup order
- **THEN** the order snapshot reflects the new branch and the change is logged

#### Scenario: Post-shipment correction blocked

- **WHEN** an admin attempts to change the branch on a shipped order
- **THEN** the update is rejected and the snapshot is unchanged

### Requirement: Frozen after shipped

Once `OrderStatus=Shipped`, no order field SHALL be editable by anyone through the application, including cancel/refund; only direct database access can alter such orders.

#### Scenario: Shipped order renders fully read-only

- **WHEN** any user views a shipped order
- **THEN** every field is read-only and no update, ship, or cancel action is offered

#### Scenario: Shipped update POST rejected

- **WHEN** an update, cancel, or refund request targets a shipped order
- **THEN** the server rejects it without modifying the order

#### Scenario: Branch without hours shows sentinel

- **WHEN** a branch has no hours information
- **THEN** every surface that shows hours displays the sentinel text `no informa` instead of hiding the row
