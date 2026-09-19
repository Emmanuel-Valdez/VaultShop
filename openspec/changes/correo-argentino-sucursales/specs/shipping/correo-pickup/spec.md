## Purpose

Lets shoppers choose a nearby Correo Argentino branch as the delivery destination at checkout, with free shipping baked into product prices.

## ADDED Requirements

### Requirement: Checkout requires a pickup agency selection

The checkout SHALL collect the customer's shipping address (Name, StreetAddress, City, State, PostalCode, PhoneNumber — unchanged) and SHALL require the shopper to select exactly one Correo Argentino branch as the delivery destination. The branch picker SHALL be the only delivery mode in this change (no domicilio toggle). The order SHALL NOT be placed when no branch is selected.

#### Scenario: Summary shows branch picker
- **WHEN** an authenticated shopper opens `Cart/Summary` with items in the cart
- **THEN** the ShippingDetails column shows the address form and a "Retiro en sucursal — Envío gratis" branch selector with a "Buscar sucursales cercanas" action

#### Scenario: Submit without branch is rejected
- **WHEN** the shopper submits `SummaryPOST` without a selected `PickupAgencyCode`
- **THEN** validation fails, the order is not created, and an error is shown at the branch selector

#### Scenario: Branch selection is required even with prefilled address
- **WHEN** `ApplicationUser` already has a saved address that prefills the form
- **THEN** the branch selector is still empty and the shopper must search and pick a branch before placing the order

### Requirement: Georef AR resolves the customer's address to coordinates

The system SHALL geocode the address the shopper typed in `Summary` via the official Georef API (`apis.datos.gob.ar/georef/api/direcciones`) to obtain coordinates for nearest-branch ranking. The call SHALL include `direccion` (street + number) and `provincia`/`localidad` when available. A failure or missing coordinate SHALL NOT crash checkout; the system SHALL fall back to a province-filtered list (no distance ordering) and still require a branch selection.

#### Scenario: Address resolves via Georef and ranking is distance-ordered
- **WHEN** the shopper enters "San Martin 123, Godoy Cruz, Mendoza" and triggers branch search
- **THEN** the system calls Georef with `direccion=San Martin 123&provincia=Mendoza&localidad=Godoy Cruz` and uses the returned `lat/lon` to rank branches by haversine distance

#### Scenario: Georef returns no coordinates
- **WHEN** Georef returns no `ubicacion` for the address
- **THEN** the branch search still returns candidates filtered by `State`/province (and locality when useful) without distance ordering and without error, and the shopper can still pick one

#### Scenario: Georef is unreachable
- **WHEN** the Georef request times out or returns non-2xx
- **THEN** the system treats it as "no coordinates" and falls back as above, logging the failure without exposing internals

### Requirement: Branch search returns the 5 nearest agencies

The system SHALL return at most 5 candidate branches ordered by haversine distance from the geocoded point. Candidates SHALL be drawn from `PostalAgency` and SHALL be filtered by the customer's `State`/province when that province has agencies. Distance in kilometers SHALL be shown per candidate. Branches beyond the 5 nearest SHALL NOT be shown.

#### Scenario: 5 nearest within province
- **WHEN** the geocoded point is in Mendoza and 20 Mendoza branches exist
- **THEN** the result is exactly 5 branches of Mendoza ordered ascending by haversine distance, each with name, street + number, locality, province, and distance

#### Scenario: Few branches in province
- **WHEN** the geocoded province has fewer than 5 branches
- **THEN** all branches of that province are returned (still distance-ordered when coordinates are available)

#### Scenario: Distance is displayed
- **WHEN** results are distance-ordered
- **THEN** each candidate shows its distance (e.g., "2.4 km") derived from the haversine calculation

### Requirement: Order persists the chosen agency as a snapshot

On `SummaryPOST` the system SHALL persist `DeliveryType=S` and the chosen branch's `PickupAgencyCode`, `PickupAgencyName`, and `PickupAgencyAddress` (street + number, locality, province, postal code as a single string) on `OrderHeader`. The snapshot SHALL NOT be a live FK to `PostalAgency`; it SHALL be denormalized so later agency data changes do not rewrite historical orders.

#### Scenario: Order stores denormalized snapshot
- **WHEN** the shopper picks agency `B1650` "San Martin" and places the order
- **THEN** the created `OrderHeader` has `DeliveryType=S`, `PickupAgencyCode=B1650`, `PickupAgencyName=San Martin`, `PickupAgencyAddress` containing its address, and the customer's domicile fields remain populated

#### Scenario: Historical order unaffected by later agency changes
- **WHEN** a `PostalAgency` row is later updated or reseeded
- **THEN** existing orders keep their original snapshot values

### Requirement: Admin and customer can see the pickup agency on the order

Order details for Admin and the customer's order history SHALL display the pickup agency (code, name, address) when `DeliveryType=S`. Admin list/detail views SHALL include the agency alongside the customer's address.

#### Scenario: Admin order detail shows agency
- **WHEN** an admin opens an order with `DeliveryType=S`
- **THEN** the detail view shows "Retiro en sucursal" with the agency code, name, and address next to the shipping address

#### Scenario: Domicile-only orders show no agency block
- **WHEN** an order predates this change and has no agency snapshot
- **THEN** no agency block is rendered and the page does not error

### Requirement: Branch data is a seeded static snapshot with coordinates

The system SHALL seed `PostalAgency` from a static `sucursales.json` snapshot that includes lat/lon per branch. The snapshot SHALL be treated as the source of truth until a live PAQ.AR feed replaces it. Reseeding SHALL be idempotent by `Code`.

#### Scenario: Seeded agencies have coordinates
- **WHEN** migrations/seed run
- **THEN** `PostalAgency` contains the national snapshot and every row has non-null `Latitude`/`Longitude`

#### Scenario: Reseed is idempotent
- **WHEN** the snapshot is reseeded a second time
- **THEN** agencies are upserted by `Code` without duplicating rows

### Requirement: Shipping to branch is free — messaging only

The system SHALL display "Envío gratis a sucursal" at the branch selector. This change SHALL NOT introduce a shipping-cost calculation, toggle, or surcharge for branch pickup; product prices already include freight.

#### Scenario: Free-shipping copy shown
- **WHEN** the shopper views the branch selector
- **THEN** the heading/label includes "Envío gratis a sucursal" (localized) and no extra shipping line appears in the order total

#### Scenario: Order total unchanged by agency choice
- **WHEN** the shopper picks any of the 5 candidates
- **THEN** `OrderTotal` is the same as before the agency feature (pricing subsystem untouched)
