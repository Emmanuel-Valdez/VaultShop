## MODIFIED Requirements

### Requirement: Automatic BxGy quantity promotions

The system SHALL support automatic buy-X-get-Y promotions (e.g. 2x1 = buy 1 get 1 free, 3x2 = buy 3 get 2 free, second unit 50% off) scoped to a product, category, keyword collection, or whole store, with a date window. Evaluation grants the free/discounted units to the cheapest eligible units first, granting EVERY earned group — a cart holding N full groups receives N times the per-group benefit. No code is required. Mixed products within the scope combine; same-SKU-only is a per-promotion option.

#### Scenario: 2x1 grants cheapest unit free
- **WHEN** a 2x1 promotion covers products A and B and the cart has A 100, A 100, B 60
- **THEN** the B 60 unit is free and the discount motive names the promotion

#### Scenario: 3x2 grants two free per five
- **WHEN** a 3x2 promotion covers a product and the cart has 5 units at 100 each
- **THEN** two units are free (300 payable) with the promotion motive

#### Scenario: Multi-group same-SKU grants each group
- **WHEN** a 2x1 promotion covers a product and the cart has 6 units at 100 each
- **THEN** the discount is 300 (three free units, 300 payable) with the promotion motive

#### Scenario: Second unit half price
- **WHEN** a "second unit 50% off" promotion applies and the cart has 2 units at 100 each
- **THEN** the discount is 50 with the promotion motive

#### Scenario: Out-of-window promotion ignored
- **WHEN** the promotion date window is not active
- **THEN** no automatic discount is applied

## ADDED Requirements

### Requirement: Coupon preserves larger per-line discounts

When a coupon wins best-price-wins at the subtotal level, its prorated share SHALL never reduce a line that already carries a larger specific discount: each line keeps `Max(existing specific discount, coupon share)`. The order-level coupon total is the sum of the winning per-line amounts (shares therefore need not sum to the nominal coupon percentage of the subtotal). Per-line motives name the discount that actually won on that line.

#### Scenario: Coupon does not shrink a larger line offer
- **WHEN** line 1 has base 10000 with a 5000 direct offer, line 2 is flat at 50000, and a 10% coupon wins at subtotal level
- **THEN** line 1 keeps its 5000 offer discount with the offer motive and only line 2 shows the coupon share

#### Scenario: Header coupon total equals sum of winners
- **WHEN** a coupon applies across lines with mixed pre-existing discounts
- **THEN** the frozen order coupon total equals the sum of the per-line winning amounts

### Requirement: Coupons apply to wholesale lines

Coupon codes SHALL apply regardless of price list: wholesale (Company role or wholesale preview) lines are eligible for coupon shares exactly like retail lines. Coupons carry no wholesale opt-in flag.

#### Scenario: Wholesale coupon applies
- **WHEN** a Company user checks out with a valid coupon
- **THEN** the coupon share applies to the wholesale-priced subtotal
