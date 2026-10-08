## Purpose

Keeps admin discount entry faithful to what the shopper sees: an offer that is saved must apply, decimal amounts must follow the request culture, and coupon and promotion selects must show words instead of enum identifiers.

## ADDED Requirements

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
