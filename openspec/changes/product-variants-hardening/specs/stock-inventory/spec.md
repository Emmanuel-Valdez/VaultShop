# product-variants-hardening — Spec delta (vs `stock-inventory`, `catalog`, `order-lifecycle`)

## MODIFIED Requirements

### Requirement: Checkout decrement is conditional on current stock

Stock decrements SHALL be conditional relative writes (`StockQuantity = StockQuantity - total WHERE StockQuantity >= total`) so that a concurrent checkout that consumed the pool causes the loser to fail with insufficient stock instead of overselling. The non-negative check constraint remains as backstop only.

#### Scenario: Loser of a last-unit race fails cleanly

- **WHEN** two checkouts race for the last unit of a product (including across different variants of it)
- **THEN** at most one order is created, the other receives insufficient stock, and stock never goes negative

### Requirement: Variants referenced by carts cannot be deleted

Deleting a variant while any shopping-cart line references it SHALL be refused with a localized message, so a cart line can never silently degrade into a base-product purchase on a variants-only product.

#### Scenario: Delete blocked by live cart reference

- **WHEN** an admin deletes a variant present in a shopper's cart
- **THEN** the delete is refused, the variant and the cart line are unchanged

### Requirement: Admin mutations are scoped to the posted product

Variant admin mutations SHALL verify the target value/variant belongs to the posted product and reject cross-product ids with a localized error instead of a success message.

### Requirement: Cart lines show their variant

Every cart-line rendering (cart page, checkout summary) SHALL display the line's variant label when present, so sibling variant lines are distinguishable.

### Requirement: Variant labels fit the column by construction

The label column SHALL fit system-built labels; model, snapshot, and migration SHALL agree on the length so overlong values fail fast instead of surfacing as misleading stock errors.
