## Purpose

Gives the store standard 2026 discount mechanics — direct sale prices, coupon codes, automatic quantity promotions (2x1, 3x2), collection-scoped offers, and payment-method discounts — with clear stacking rules and full order traceability.

## Requirements

### Requirement: Product direct offer price with date window

A product MAY carry an optional sale price per price list (retail offer, wholesale offer) with an optional start/end date. When the offer is active, the effective base price is the offer price and the original price is shown struck-through.

#### Scenario: Active retail offer applies
- **WHEN** a product has retail price 10000 and an active retail offer 8000 within its date window
- **THEN** a retail viewer sees 8000 with 10000 struck-through

#### Scenario: Expired offer ignored
- **WHEN** the offer end date is in the past
- **THEN** the viewer sees the regular price and no strikethrough is shown

#### Scenario: No offer means no badge
- **WHEN** a product has no offer price set
- **THEN** only the regular price is shown

### Requirement: Coupon codes with validation and re-validation

The system SHALL support coupon codes giving either percentage-off or fixed-amount-off the order subtotal, with optional minimum subtotal, date window, and max total uses. At most ONE coupon applies per order. The coupon is validated when applied to the cart and re-validated at order creation; an invalid coupon at creation blocks its discount but never blocks the order itself (order proceeds undiscounted with a localized notice).

#### Scenario: Percentage coupon applies
- **WHEN** a shopper applies valid code `BIENVENIDA10` (10% off, min subtotal 20000) to a 50000 subtotal cart
- **THEN** the cart shows a 5000 discount line with the code

#### Scenario: Fixed coupon capped at subtotal
- **WHEN** a 15000 fixed coupon applies to a 10000 subtotal
- **THEN** the discount is capped so the subtotal never goes negative

#### Scenario: Expired coupon rejected
- **WHEN** a shopper applies an expired or over-used coupon
- **THEN** a localized error is shown and no discount is applied

#### Scenario: Coupon re-validated at order creation
- **WHEN** a coupon valid at apply time is expired or exhausted before checkout completes
- **THEN** the order is created without that discount and the shopper sees a localized notice

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

### Requirement: Collection-scoped percentage offers

A percentage-off rule MAY target a Keyword collection and/or a Category with a date window. Eligible lines receive the percentage off their effective price.

#### Scenario: Collection offer applies to member product
- **WHEN** a 20% collection offer covers keyword 7 and the cart has a product of keyword 7 at 10000
- **THEN** the line discount is 2000 with the collection offer motive

#### Scenario: Non-member product unaffected
- **WHEN** the cart has a product outside the targeted collection/category
- **THEN** that line receives no collection discount

### Requirement: Payment-method percentage discount

A percentage discount MAY be configured per payment method (starting with bank transfer, e.g. -10%). It applies to the payment-discount-eligible subtotal AFTER line-level specific discounts, and it stacks with the single winning specific discount.

#### Scenario: Transfer discount stacks on sale price
- **WHEN** a line costs 8000 after its specific discount and the transfer discount is 10%
- **THEN** choosing bank transfer adds an 8000 * 10% = 800 order-level payment discount

#### Scenario: Online methods without discount add nothing
- **WHEN** the shopper pays with a method that has no configured discount
- **THEN** no payment-method discount line appears

### Requirement: Non-stacking rule for specific discounts with best-price-wins

At most ONE specific discount (direct offer, coupon share, BxGy share, collection offer) SHALL apply to the same line/subtotal portion: the system evaluates all eligible specific discounts and applies the one yielding the lowest payable amount. The payment-method discount stacks on top of that winner. Wholesale (Company role or wholesale preview) lines are excluded from specific discounts by default unless a promotion explicitly opts in.

#### Scenario: Best specific discount wins
- **WHEN** a line is eligible for both a 20% direct offer and a 25% collection offer
- **THEN** only the 25% discount applies to that line

#### Scenario: Coupon and auto-promo do not combine on same subtotal
- **WHEN** a cart is eligible for both a 10% coupon and a BxGy benefit
- **THEN** the order applies whichever yields the lower payable subtotal, never both

#### Scenario: Wholesale excluded by default
- **WHEN** a Company user checks out and a retail-only promotion is active
- **THEN** no specific discount applies unless the promotion explicitly includes wholesale

### Requirement: Discount traceability frozen on the order

Every order SHALL freeze its discount breakdown: coupon code used, promotion ids applied, per-line discount motive and amount, order-level discount totals. Later edits to promotions or coupons SHALL NOT rewrite past orders. Discount lines SHALL appear in admin order details, customer order view, confirmation emails, and PDF/HTML summaries.

#### Scenario: Order freezes promotion snapshot
- **WHEN** an order is created with a BxGy benefit and a transfer discount
- **THEN** the order stores both motives and amounts and they survive later promotion edits

#### Scenario: Discount visible everywhere the order is shown
- **WHEN** viewing an already-created discounted order in admin, customer history, email, or PDF
- **THEN** each discount line with its motive and amount is visible

### Requirement: Product offer fields persist

Saving a product SHALL persist the retail offer price, the wholesale offer price, and the offer start and end. Reopening the product form SHALL show the saved values. An active offer SHALL change the storefront price. An offer price SHALL be greater than zero and strictly less than the same-channel final price, and the end SHALL NOT be before the start. Empty start and end SHALL mean the offer is always active.

#### Scenario: Saved offer reloads

- **WHEN** an admin saves a retail offer of 8000 and a wholesale offer of 6000 with a start and end, then reopens the product
- **THEN** both prices and both dates are still filled

#### Scenario: Active offer is visible

- **WHEN** a product has a regular retail price of 10000 and a saved active retail offer of 8000
- **THEN** a retail shopper sees 8000 with 10000 struck through

#### Scenario: Offer above the regular price is rejected

- **WHEN** an admin saves a retail offer greater than or equal to the retail price
- **THEN** the form shows a validation error and the previous offer is unchanged

### Requirement: Admin decimals follow the request culture

Admin decimal fields for the offer prices, coupon value, coupon minimum subtotal, and promotion percentages SHALL accept a comma in `es-AR` and a dot in `en-US`, and SHALL reject the other separator. Integer fields SHALL stay whole numbers.

#### Scenario: Spanish comma is accepted

- **WHEN** an admin in `es-AR` enters `8000,50` in an offer price and saves
- **THEN** the stored amount is 8000.50 and the form redisplays `8000,50`

#### Scenario: Spanish dot is rejected

- **WHEN** an admin in `es-AR` enters `8000.50` in an offer price
- **THEN** the value is not saved as eight thousand and a validation error is shown

#### Scenario: English dot is accepted

- **WHEN** an admin in `en-US` enters `8000.50` in a coupon value and saves
- **THEN** the stored amount is 8000.50 and the form redisplays `8000.50`

### Requirement: Coupon and promotion selects are localized

The coupon type select and the promotion kind and scope selects SHALL show localized labels in `es-AR` and `en-US`. The stored values SHALL remain the existing enum values.

#### Scenario: Spanish coupon types

- **WHEN** an admin opens the coupon form in `es-AR`
- **THEN** the type options read `Porcentual` and `Monto fijo`

#### Scenario: English coupon types

- **WHEN** an admin opens the coupon form in `en-US`
- **THEN** the type options read `Percent` and `Fixed amount`

#### Scenario: Spanish promotion kind and scope

- **WHEN** an admin opens the promotion form in `es-AR`
- **THEN** the kind options read `Llevá X, pagá Y`, `Porcentaje`, and `Descuento por medio de pago`, and the scope options read `Tienda`, `Producto`, `Categoría`, and `Colección`

#### Scenario: English promotion kind and scope

- **WHEN** an admin opens the promotion form in `en-US`
- **THEN** the kind options read `Buy X, pay Y`, `Percent off`, and `Payment method discount`, and the scope options read `Store`, `Product`, `Category`, and `Collection`

### Requirement: Admin windows use Argentina time

The system SHALL interpret admin-entered offer, coupon, and promotion start/end wall times as `America/Argentina/Buenos_Aires` time (Windows fallback `Argentina Standard Time`) when persisting, and SHALL convert stored UTC back to Argentina time when redisplaying the admin form. Storage and window evaluation SHALL remain UTC.

#### Scenario: Saved window stores the correct UTC instant

- **WHEN** an admin enters an offer window of `18:00`–`23:00` Argentina time
- **THEN** the stored UTC values are `21:00`–`02:00Z` (UTC-3 offset) and evaluation against `DateTime.UtcNow` activates the offer at `21:00Z`

#### Scenario: Reopened form shows the entered wall time

- **WHEN** an admin saves a window and reopens the product, coupon, or promotion form
- **THEN** the start and end inputs show the same Argentina wall time that was entered

#### Scenario: Midnight boundary converts to the next UTC day

- **WHEN** an admin enters an offer ending at `00:30` Argentina time
- **THEN** the stored UTC end falls on the next calendar day (`03:30Z`) and the window evaluates accordingly

### Requirement: Storefront offer deadline displays in Argentina time

The storefront offer-deadline render SHALL show the Argentina wall time of `OfferEndUtc` in the visible `OfferEnd` text while the `<time datetime>` attribute SHALL carry the UTC instant. When the product has no offer end, the render SHALL emit nothing extra.

#### Scenario: Deadline shows Argentina time

- **WHEN** a product has an active direct offer ending at `21:00Z`
- **THEN** the shopper sees the `OfferEnd` text with `18:00` and the `<time datetime>` attribute holds the `21:00Z` instant

#### Scenario: No deadline renders nothing extra

- **WHEN** a product has no offer end date
- **THEN** the price badge renders with no clock line beneath it
