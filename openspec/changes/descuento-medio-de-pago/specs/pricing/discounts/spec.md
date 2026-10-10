## Purpose

Payment sessions charge exactly the order total, and the shopper sees any payment-method discount before confirming. Stacking rules are documented where shoppers ask.

## ADDED Requirements

### Requirement: Payment session charges the order total

When a payment-method discount is active, the payment session request SHALL distribute `PaymentDiscountTotal` across the session line items (whole cents summing exactly to `OrderTotal`) for the session request only. Persisted `OrderDetail` rows and the order header SHALL remain unchanged. When no payment-method discount is active, session construction SHALL behave exactly as before.

#### Scenario: Session matches the discounted total

- **WHEN** an order totals 9000 after a 1000 bank-transfer discount and the shopper pays by card/MP equivalent session
- **THEN** the session line items sum to 9000, not to the pre-discount 10000

#### Scenario: No payment discount means unchanged session

- **WHEN** no payment-method discount applies to the order
- **THEN** the session line items are built exactly as before this change

### Requirement: Payment-method discount visible before confirmation

The payment-method picker SHALL announce the active discount on the applicable method card (computed from active promotions, never hardcoded). Choosing a method on the Summary SHALL re-evaluate (via the existing summary evaluation with the selected method) and update the discount row and final total BEFORE Place Order. A note under the selected method SHALL state that method's current offer.

#### Scenario: Picker advertises the transfer discount

- **WHEN** a 10% bank-transfer promotion is active and the shopper opens the Summary
- **THEN** the bank-transfer card announces the −10% benefit while methods without a promotion show no announcement

#### Scenario: Selecting a method updates totals pre-confirmation

- **WHEN** the shopper selects bank transfer on the Summary
- **THEN** the payment-discount row appears with the server-computed amount and the final total drops accordingly before Place Order

#### Scenario: Selected-method note states the offer

- **WHEN** the shopper selects a method carrying an active payment-method promotion
- **THEN** a note under the picker states that method's current offer; selecting a method without one shows no note

### Requirement: FAQ documents discount stacking

The FAQ page SHALL include a Q11 item (plus `Question11`/`Answer11` keys in both cultures) stating that specific discounts never stack (best price wins) while the payment-method discount stacks on top of the winner, matching evaluator behavior.

#### Scenario: Shopper reads the stacking answer

- **WHEN** a shopper opens FAQ Q11 in Spanish or English
- **THEN** it explains specifics do not combine (best price wins) and the payment-method discount applies on top
