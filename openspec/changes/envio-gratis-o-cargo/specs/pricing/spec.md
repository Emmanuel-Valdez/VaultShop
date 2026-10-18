## ADDED Requirements

### Requirement: Suggested retail includes shipping only for free-shipping products

The price calculator SHALL include the category average shipping cost in a product's suggested retail price only when the product carries the free-shipping flag, using the same computation as today: the shipping share is added to the retail cost basis before the percentage-cost gross-up, and the profit margin is not applied to it. For products without the flag, the suggested retail SHALL exclude the shipping share so the shipping can be charged separately at checkout. The suggested wholesale price SHALL remain unaffected by the flag. Publishing suggested prices SHALL persist the resulting retail and wholesale values as it does today.

#### Scenario: Flagged product includes the shipping share

- **WHEN** the calculator computes the suggested retail for a free-shipping product in a category with average shipping cost 5000 and the configured total percentage costs are 10%
- **THEN** the suggested retail exceeds the same product's suggestion without shipping by 5555.56

#### Scenario: Unflagged product excludes the shipping share

- **WHEN** the calculator computes the suggested retail for a product without the free-shipping flag
- **THEN** the category average shipping cost is not part of the suggested retail for that product

#### Scenario: Publishing persists the per-flag suggestions

- **WHEN** an admin publishes suggested prices for a mix of flagged and unflagged products
- **THEN** each product's published retail price reflects whether its flag includes or excludes the category shipping share, and wholesale prices are written unchanged by the flag

#### Scenario: Malformed expectations still block pricing

- **WHEN** the calculator evaluates products and one product has an invalid monthly expectation
- **THEN** the existing pricing guards still apply and no suggestion is produced
