## ADDED Requirements

### Requirement: Unavailable variants leave the cart

Opening the cart and building the checkout summary SHALL remove a line whose variant is missing, belongs to another product, or is unavailable, using the same cleanup as a product that was deleted or marked unavailable. Checkout SHALL still refuse to create an order if an unavailable variant is present at creation time.

#### Scenario: Disabled variant is removed on cart open

- **WHEN** a shopper has a variant in the cart and an admin marks that variant unavailable, then the shopper opens the cart
- **THEN** that line is gone and the remaining lines stay

#### Scenario: Checkout does not sell a raced unavailable variant

- **WHEN** a variant becomes unavailable after the summary was built and before the order is created
- **THEN** no order is created for that line
