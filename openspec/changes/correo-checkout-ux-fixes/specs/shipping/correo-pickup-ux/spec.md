## Purpose

Makes the Correo Argentino branch picker at checkout resilient to failure: typed input survives validation errors, candidates survive postback, and search/submit give clear feedback.

## ADDED Requirements

### Requirement: Failed checkout preserves typed input

When `SummaryPOST` re-renders `Summary` after a validation failure (missing/invalid branch or `ModelState` invalid), the system SHALL redisplay the shopper's posted `OrderHeader` fields (name, phone, street, city, state, postal code, payment method, picked code) instead of a fresh blank form. The cart lines and totals SHALL be rebuilt from the current cart.

#### Scenario: Submit without branch keeps address
- **WHEN** the shopper fills the address, skips branch search, and submits
- **THEN** the re-rendered form still shows the typed name, phone, address, city, state, postal code, and payment choice, with the branch-required error at the selector

#### Scenario: Invalid model keeps payment choice
- **WHEN** `ModelState` is invalid on POST
- **THEN** the re-rendered form preserves the posted payment method and address fields

### Requirement: Candidates survive a failed submit

When `SummaryPOST` fails validation, the system SHALL re-run branch search server-side using the posted street/city/state (same geocode + nearest-5 path as `GetNearestAgencies`) and SHALL render those candidates with the posted `PickupAgencyCode` pre-checked when it is still a valid candidate.

#### Scenario: Missing branch re-shows candidates
- **WHEN** the shopper searched, picked nothing, and submitted
- **THEN** the re-rendered picker lists the same candidates again (up to 5, same ordering) with none checked and the required error visible

#### Scenario: Invalid code keeps candidate list
- **WHEN** the shopper posts a forged or stale `PickupAgencyCode`
- **THEN** no order is created, the picker re-lists the fresh candidates, and the branch-required error is shown

### Requirement: Branch search gives clear feedback

The branch search SHALL require street and state before fetching, SHALL disable the search button and announce "searching" status while in flight, SHALL cancel a stale in-flight request when a new search starts, and SHALL show the no-results copy on empty results or fetch/HTTP failure without throwing.

#### Scenario: Empty address blocks search
- **WHEN** the shopper clicks search with blank street or state
- **THEN** no fetch is issued and a prompt to complete the address is shown

#### Scenario: Search disables during fetch
- **WHEN** a branch search is in flight
- **THEN** the search button is disabled until the request settles, and a second click does not issue a parallel fetch

#### Scenario: Fetch failure shows no-results copy
- **WHEN** the agencies endpoint returns non-OK or the network fails
- **THEN** the picker shows the localized no-branches-found copy and no candidates

### Requirement: Submit is gated on branch selection client-side

The `Place Order` submit SHALL stay disabled until a branch radio is checked. Checking any candidate SHALL enable it. Server-side branch validation SHALL remain authoritative (a forged POST with no/invalid code still fails with the branch error).

#### Scenario: No selection disables submit
- **WHEN** the picker has no checked radio (initial load or after a search with no pick)
- **THEN** the submit button is disabled

#### Scenario: Pick enables submit
- **WHEN** the shopper checks any candidate radio
- **THEN** the submit button becomes enabled

#### Scenario: No-JS still enforces server rule
- **WHEN** a client without JavaScript submits with no branch
- **THEN** the server rejects with the branch-required error and no order is created
