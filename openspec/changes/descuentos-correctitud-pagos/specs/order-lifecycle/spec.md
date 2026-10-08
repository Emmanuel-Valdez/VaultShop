## ADDED Requirements

### Requirement: Frozen line prices sum to the header total

Persisted order line prices SHALL be whole cents, and the sum of frozen line totals SHALL equal the frozen order total: line unit prices round to 2 decimals with any rounding remainder absorbed by the last line of the same product, so admin details, customer history, emails, and PDF/HTML summaries never show a line column that disagrees with the header total.

#### Scenario: Split totals reconcile to the cent
- **WHEN** a discounted order total splits unevenly across 3 units (e.g. 200 over 3 units)
- **THEN** the persisted unit prices are whole cents and unit price times count sums exactly to the header total

### Requirement: Orders freeze the payment-discount motive

Every order created with a payment-method discount SHALL persist the discount motive alongside the amount, exactly like per-line motives. Later edits to the promotion SHALL NOT change past orders, and all four order surfaces SHALL render the frozen motive with its amount.

#### Scenario: Payment motive frozen and rendered
- **WHEN** an order is created with a bank-transfer discount from a promotion later renamed or deleted
- **THEN** admin details, customer history, confirmation email, and PDF/HTML summary still show the motive and amount captured at purchase

### Requirement: Coupon use increment is atomic

Concurrent checkouts consuming the last use of a limited coupon SHALL grant the discount to exactly one order: the use-count increment is a single conditional write, and the loser proceeds undiscounted with the existing localized coupon-dropped notice, never blocked.

#### Scenario: Last-use race grants one order
- **WHEN** two checkouts race for the final use of a coupon with one remaining use
- **THEN** exactly one order carries the coupon discount and the other is created undiscounted with the coupon-dropped notice
