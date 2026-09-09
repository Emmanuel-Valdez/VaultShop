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

The system SHALL prevent adding more units of a product to a cart than are available in stock, considering the user's existing cart quantity for that product.

#### Scenario: First add within stock succeeds
- **WHEN** an authenticated user adds `Count = 3` of a product with `StockQuantity = 5` and no existing cart line for that product
- **THEN** the cart line is created with `Count = 3`

#### Scenario: Add that would exceed stock is rejected
- **WHEN** a user already has `2` units in cart for a product with `StockQuantity = 5` and tries to add `4` more (total 6)
- **THEN** the request is rejected with a localized stock-error message and the cart line remains at `2`

#### Scenario: Zero-stock product cannot be added
- **WHEN** a user tries to add any quantity of a product with `StockQuantity = 0`
- **THEN** the request is rejected with a localized out-of-stock message

#### Scenario: Unauthenticated add still requires sign-in (no stock bypass)
- **WHEN** an unauthenticated user posts to `HomeController.Details` with any count
- **THEN** the system challenges authentication before any stock check or cart mutation

### Requirement: Cart quantity adjustments respect stock

The system SHALL enforce stock limits when the user increments cart quantity via `CartController.Plus` and SHALL allow decrement/removal regardless of stock.

#### Scenario: Plus within stock succeeds
- **WHEN** a cart line has `Count = 2` for a product with `StockQuantity = 5` and the user triggers `Plus`
- **THEN** the count becomes `3`

#### Scenario: Plus that would exceed stock is rejected
- **WHEN** a cart line already equals available stock (`Count = 5`, `StockQuantity = 5`) and the user triggers `Plus`
- **THEN** the request is rejected with a localized stock-limit message and `Count` remains `5`

#### Scenario: Minus and Remove always succeed
- **WHEN** the user triggers `Minus` or `Remove` on any cart line
- **THEN** the count is decremented or the line removed without a stock check

### Requirement: Checkout validates stock atomically and decrements on order creation

The system SHALL validate the entire cart against current `StockQuantity` inside the same transaction that creates the order, decrement `StockQuantity` for each product when the order is created, and fail the checkout without creating a partial order if any line exceeds stock.

#### Scenario: Checkout succeeds and decrements stock
- **WHEN** a user checks out with a cart containing `2 x Product A (Stock 10)` and `1 x Product B (Stock 3)`
- **THEN** an `OrderHeader` + `OrderDetail` rows are created, `Product A` stock becomes `8`, `Product B` stock becomes `2`, and the cart is cleared

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
