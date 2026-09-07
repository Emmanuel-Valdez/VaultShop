## MODIFIED Requirements

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
