## Purpose

The amount actually charged always equals the frozen order total shown to the shopper and persisted on the order.

## ADDED Requirements

### Requirement: Charged session amount equals the frozen order total

The amount submitted to the online payment session SHALL equal the frozen `OrderTotal` persisted on the order header, including any payment-method discount. Any per-line split used for the session request SHALL sum exactly to that total and SHALL NOT alter persisted `OrderDetail` rows.

#### Scenario: Charged amount matches the header

- **WHEN** an order is created with a payment-method discount and proceeds to Stripe/MP
- **THEN** the charged session amount equals the persisted `OrderTotal` and the persisted line rows are unchanged
