## MODIFIED Requirements

### Requirement: Cart and checkout use the same unified price

Cart totals and order creation SHALL price each line item using the same unified rule as storefront display: (1) base role price (wholesale when the actor is a company user or an admin/employee in wholesale preview, otherwise retail); (2) the single best eligible specific discount per line/subtotal (best-price-wins, never stacked; a coupon share never reduces a line holding a larger specific discount); (3) the payment-method discount applied to the eligible subtotal after line discounts. Persisted line unit prices SHALL be whole cents summing exactly to the order total. The cart summary and checkout summary SHALL reflect that resolved price with visible discount lines and motives, and the coupon SHALL be re-validated at order creation.

#### Scenario: Company checkout uses wholesale
- **WHEN** a Company user builds a cart summary and creates an order
- **THEN** each cart line and the order total are calculated from the wholesale price

#### Scenario: Admin in wholesale preview checkout uses wholesale
- **WHEN** an admin or employee in wholesale preview builds a cart summary and creates an order
- **THEN** each cart line and the order total are calculated from the wholesale price

#### Scenario: Retail checkout uses retail
- **WHEN** a retail customer or an admin in retail preview builds a cart summary and creates an order
- **THEN** each cart line and the order total are calculated from the retail price

#### Scenario: Discounted checkout shows motive lines
- **WHEN** a retail cart qualifies for a BxGy benefit and the transfer payment discount
- **THEN** the summary shows the BxGy line discount with its motive plus the payment-method discount line, and the order freezes both
