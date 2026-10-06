## Purpose

Prevents overselling by giving every product a tracked stock quantity, letting admins manage it, and enforcing it at add-to-cart and checkout with an atomic decrement.

## Requirements

### Requirement: Product stock quantity is persisted

The system SHALL persist a non-negative integer `StockQuantity` on every `Product` (default 0) and surface it through queries used by storefront, cart, and checkout.

#### Scenario: New product defaults to zero stock
- **WHEN** an admin creates a product without specifying stock
- **THEN** the persisted `StockQuantity` is `0`

#### Scenario: Existing products have a stock value after migration
- **WHEN** the migration adding `StockQuantity` runs against a database with existing products
- **THEN** every existing product has a defined `StockQuantity` (>= 0)

#### Scenario: Stock survives soft-delete and availability toggles
- **WHEN** a product is soft-deleted or marked unavailable in store
- **THEN** its `StockQuantity` remains stored and is included when the product is restored

### Requirement: Admin can view and edit stock

The system SHALL allow users in `Admin`/`Employee` roles to view and edit `StockQuantity` on the product admin list and upsert form, with server-side validation.

#### Scenario: Admin list shows stock
- **WHEN** an admin loads the product list (`ProductController.GetAll`)
- **THEN** each product row includes its current `StockQuantity`

#### Scenario: Admin sets stock on create
- **WHEN** an admin creates a product with `StockQuantity = 15`
- **THEN** the product is persisted with `StockQuantity = 15`

#### Scenario: Admin updates stock on edit
- **WHEN** an admin edits an existing product and changes `StockQuantity` to `7`
- **THEN** the updated value is persisted

#### Scenario: Negative stock is rejected server-side
- **WHEN** an admin submits `StockQuantity < 0`
- **THEN** model validation fails and the product is not saved

### Requirement: Add-to-cart respects available stock

The system SHALL prevent adding more units of a product to a cart than are available in stock, considering the user's existing cart quantity for that product summed across all of its variant lines. Stock remains a single shared pool on the product; variants carry no stock of their own.

#### Scenario: First add within stock succeeds
- **WHEN** an authenticated user adds `Count = 3` of a product with `StockQuantity = 5` and no existing cart line for that product
- **THEN** the cart line is created with `Count = 3`

#### Scenario: Add that would exceed stock is rejected
- **WHEN** a user already has `2` units in cart for a product with `StockQuantity = 5` and tries to add `4` more (total 6)
- **THEN** the request is rejected with a localized stock-error message and the cart line remains at `2`

#### Scenario: Variant lines sum against the shared pool

- **WHEN** a user has `Gryffindor / 15"` x2 and `Slytherin / 17"` x2 of a product with `StockQuantity = 5` and tries to add 2 more of any variant (total 6)
- **THEN** the request is rejected with a localized stock-error message and both lines keep their counts

#### Scenario: Zero-stock product cannot be added
- **WHEN** a user tries to add any quantity of a product with `StockQuantity = 0`
- **THEN** the request is rejected with a localized out-of-stock message

#### Scenario: Unauthenticated add still requires sign-in (no stock bypass)
- **WHEN** an unauthenticated user posts to `HomeController.Details` with any count
- **THEN** the system challenges authentication before any stock check or cart mutation

### Requirement: Cart quantity adjustments respect stock

The system SHALL enforce stock limits when the user increments cart quantity via `CartController.Plus`, comparing the product's total quantity across all of its variant lines plus one against the shared `StockQuantity`, and SHALL allow decrement/removal regardless of stock.

#### Scenario: Plus within stock succeeds
- **WHEN** a cart line has `Count = 2` for a product with `StockQuantity = 5` and the user triggers `Plus`
- **THEN** the count becomes `3`

#### Scenario: Plus that would exceed stock is rejected
- **WHEN** a cart line already equals available stock (`Count = 5`, `StockQuantity = 5`) and the user triggers `Plus`
- **THEN** the request is rejected with a localized stock-limit message and `Count` remains `5`

#### Scenario: Plus accounts for sibling variant lines

- **WHEN** a product with `StockQuantity = 5` has variant lines x2 and x3 and the user triggers `Plus` on either line (total would become 6)
- **THEN** the request is rejected with a localized stock-limit message and both lines keep their counts

#### Scenario: Minus and Remove always succeed
- **WHEN** the user triggers `Minus` or `Remove` on any cart line
- **THEN** the count is decremented or the line removed without a stock check

### Requirement: Checkout validates stock atomically and decrements on order creation

The system SHALL validate the entire cart against current `StockQuantity` inside the same transaction that creates the order, summing quantities per product across all of its variant lines, decrement `StockQuantity` once per product by that summed total when the order is created, and fail the checkout without creating a partial order if any product total exceeds stock.

#### Scenario: Checkout succeeds and decrements stock
- **WHEN** a user checks out with a cart containing `2 x Product A (Stock 10)` and `1 x Product B (Stock 3)`
- **THEN** an `OrderHeader` + `OrderDetail` rows are created, `Product A` stock becomes `8`, `Product B` stock becomes `2`, and the cart is cleared

#### Scenario: Multi-variant checkout decrements the shared pool once

- **WHEN** a user checks out with `Gryffindor / 15"` x2 plus `Slytherin / 17"` x1 of a product with `StockQuantity = 5`
- **THEN** the order is created with two `OrderDetail` rows and the product stock becomes `2` via a single decrement of 3

#### Scenario: Checkout fails if any line exceeds stock
- **WHEN** a user checks out where one cart line requests `6` but only `5` remain (even if other lines are valid)
- **THEN** no order is created, no stock is decremented, and the user receives a localized insufficient-stock error

#### Scenario: Concurrent checkouts do not oversell
- **WHEN** two users concurrently check out the last `1` unit of the same product
- **THEN** at most one order succeeds; the other fails with an insufficient-stock error and stock never goes negative

#### Scenario: Stock validation includes products that became unavailable
- **WHEN** a cart contains a product that was soft-deleted or marked unavailable since it was added
- **THEN** the outdated cart handling removes it before stock validation and checkout proceeds only with valid lines (or fails as empty-cart if none remain)

#### Scenario: Company delayed-payment orders also decrement stock
- **WHEN** a user in `Company` role checks out (order enters `Pending` + `DelayedPayment`, not `Approved`)
- **THEN** stock is still validated and decremented in the same transaction as order creation and the order remains `Pending` until payment is approved

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

#### Scenario: Cross-product id is rejected

- **WHEN** an admin posts a value or variant id that belongs to a different product than the posted product id
- **THEN** the mutation is refused with a localized error and nothing is changed

### Requirement: Cart lines show their variant

Every cart-line rendering (cart page, checkout summary) SHALL display the line's variant label when present, so sibling variant lines are distinguishable.

#### Scenario: Sibling lines are distinguishable

- **WHEN** a cart holds two lines of the same product with different variants
- **THEN** each line shows its variant label on both the cart page and the checkout summary

### Requirement: Variant labels fit the column by construction

The label column SHALL fit system-built labels; model, snapshot, and migration SHALL agree on the length so overlong values fail fast instead of surfacing as misleading stock errors.

#### Scenario: Long system label persists without truncation

- **WHEN** an order line carries the longest system-built variant label
- **THEN** it is stored and displayed in full with no database truncation error
