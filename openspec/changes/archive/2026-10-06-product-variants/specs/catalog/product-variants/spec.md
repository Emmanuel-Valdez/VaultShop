## Purpose

Lets each product offer customer-choosable options (size, color, house, capacity, material, model, or any custom option) as explicit purchasable combinations without duplicating the product, while keeping one price, one stock pool, and one photo gallery per product.

## ADDED Requirements

### Requirement: Variant option types are generic and reusable

The system SHALL allow admins to define variant option-type names freely (no hardcoded Size/Color set). Type names are reusable across products; each product defines its own values under the types it uses. A product with no variants SHALL behave exactly as today.

#### Scenario: Admin creates a custom option type

- **WHEN** an admin adds type `Casa` with values `Gryffindor, Slytherin, Ravenclaw, Hufflepuff` to the Harry Potter backpack
- **THEN** the product offers `Casa` as a selectable option with those four values

#### Scenario: Same type name, different values per product

- **WHEN** the water bottle defines `Color = Black/Blue/Red` and the t-shirt defines `Color = Black/White`
- **THEN** each product offers only its own values; neither product sees the other's values

#### Scenario: Product without variants is unaffected

- **WHEN** a product has no variant types or combinations defined
- **THEN** its detail page, cart behavior, pricing, and stock behave exactly as before this change

### Requirement: Purchasable combinations are explicit rows

Each purchasable variant SHALL be an explicit combination holding exactly one value of every option type the product uses. Each combination SHALL be individually available or unavailable. An unavailable combination SHALL block new adds to cart; existing carts and past orders referencing it SHALL remain intact.

#### Scenario: Two-type product yields explicit combinations

- **WHEN** the backpack uses `Casa` (4 values) and `Tamaño` (2 values) and combinations are generated
- **THEN** 8 explicit combinations exist (e.g. `Gryffindor / 15"`, `Slytherin / 17"`), each selectable and each individually toggleable

#### Scenario: Single-type product yields one combination per value

- **WHEN** a product uses only `Talle` with values `S/M/L`
- **THEN** 3 combinations exist, one per value

#### Scenario: Disabling one combination does not affect others

- **WHEN** an admin disables `Slytherin / 17"` while 7 other combinations stay available
- **THEN** new adds of that combination are rejected, the other 7 remain purchasable, and any existing cart line or past order for it keeps its data

### Requirement: Admin generates combinations with a simple UI

The admin product editor SHALL let admins define values per option type and generate combinations from them (cartesian product of the defined values), then prune or toggle individual rows. Generation is a creation helper only: the system SHALL persist explicit combination rows, never a rule. No bulk-editing features are required.

#### Scenario: Generate then prune

- **WHEN** an admin defines `Casa` (4 values) + `Tamaño` (2 values) and triggers generation
- **THEN** 8 combination rows are created, and the admin can delete or disable any row (e.g. remove a combination that is not sold)

#### Scenario: Values are defined once and reused across combinations

- **WHEN** value `15"` is defined under `Tamaño`
- **THEN** it is stored once and referenced by every combination that includes it (e.g. `Gryffindor / 15"`, `Slytherin / 15"`)

#### Scenario: Deleting a value is guarded

- **WHEN** an admin tries to delete a value that is referenced by at least one combination
- **THEN** the delete is blocked with a message until the referencing combinations are removed or the value is kept

### Requirement: Cart lines are keyed by variant

The cart identity SHALL be `(user, product, variant)`: the same product with different variants lives on separate cart lines; adding the same product with the same variant to an existing line SHALL merge by increasing `Count`. Variant-less products keep single-line behavior as today.

#### Scenario: Different variants are separate lines

- **WHEN** a user adds backpack `Gryffindor / 15"` x1 and backpack `Slytherin / 17"` x1
- **THEN** the cart shows two lines, one per variant

#### Scenario: Same variant merges

- **WHEN** a user with `Gryffindor / 15"` x1 in cart adds `Gryffindor / 15"` x2 more (within stock)
- **THEN** the existing line becomes x3; no second line is created

#### Scenario: Variant-less product behavior unchanged

- **WHEN** a user adds a product with no variants twice
- **THEN** the single line merges as before this change

### Requirement: Single price and single gallery across variants

All variants of a product SHALL sell at the product's price (retail/wholesale resolution unchanged) and share the product's photo gallery. No per-variant price, stock, or images exist in v1.

#### Scenario: Variant does not change price

- **WHEN** a cart line or order line carries any variant of a product
- **THEN** its unit price equals the product's resolved retail or wholesale price for that user

#### Scenario: Variant does not change gallery

- **WHEN** a shopper selects different variants on the detail page
- **THEN** the displayed photos remain the product gallery
