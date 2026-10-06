## ADDED Requirements

### Requirement: Orders freeze the discount breakdown

Every order created from a discounted cart SHALL persist the discount breakdown alongside prices: coupon code (if any), applied promotion ids, per-line discount motive and amount, and order-level discount totals (specific discounts + payment-method discount). Past orders SHALL remain unchanged when promotions or coupons are later edited, disabled, or deleted.

#### Scenario: Discounted order persists breakdown
- **WHEN** a shopper completes checkout with a coupon and a transfer discount
- **THEN** the order stores the coupon code, both discount amounts, and per-line motives

#### Scenario: Promotion edits do not rewrite history
- **WHEN** an admin later changes or deletes an applied promotion
- **THEN** existing orders still show the motive and amounts captured at purchase

#### Scenario: Discounts visible on order views and documents
- **WHEN** viewing the order in admin details, customer history, confirmation email, or PDF/HTML summary
- **THEN** each discount line with its motive and amount is shown
