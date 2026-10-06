## ADDED Requirements

### Requirement: OrderDetail variant snapshot is immutable

Every `OrderDetail` row created from a cart line with a variant SHALL persist the variant reference plus a frozen human-readable `VariantLabel` text snapshot (e.g. `Casa: Gryffindor, Tamaño: 15"`), captured at order creation like `Price`. Renaming, disabling, or deleting the variant afterwards SHALL NOT alter past orders. Frozen-after-shipped semantics extend to the variant label, and the label SHALL be shown wherever order lines are displayed (order details, confirmation, summary HTML/PDF, emails).

#### Scenario: Order captures variant snapshot

- **WHEN** a user checks out a cart line for variant `Gryffindor / 15"`
- **THEN** its `OrderDetail` row stores the variant reference and a `VariantLabel` describing the selected values

#### Scenario: Rename does not rewrite history

- **WHEN** an admin later renames value `Black` to `Negro`
- **THEN** existing `OrderDetail` rows still show the label captured at purchase

#### Scenario: Disabled variant keeps history readable

- **WHEN** an order references a variant that is later disabled or deleted
- **THEN** the order still displays its frozen `VariantLabel`

#### Scenario: Variant-less order lines unchanged

- **WHEN** an order line comes from a product with no variants
- **THEN** no variant label is stored or displayed for that line
