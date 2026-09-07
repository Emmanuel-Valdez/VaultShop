# Stock Visibility Specification

## Purpose

Makes product stock availability visible to shoppers before and during purchase decisions: out-of-stock products are signaled on product cards without opening the product page, the available quantity is shown on the detail page, and structured-data availability metadata matches reality for search engines.

## ADDED Requirements

### Requirement: Out-of-stock banner on product cards
Storefront product cards on the Home and Search pages SHALL render a translucent "out of stock" banner over the card image when the product's stock quantity is zero. The banner MUST include localized text (not color or icon alone) and MUST NOT appear when stock is greater than zero.

#### Scenario: Card for out-of-stock product
- **WHEN** a product card is rendered for a product with stock quantity 0
- **THEN** the card image area shows a translucent banner with the localized out-of-stock label

#### Scenario: Card for in-stock product
- **WHEN** a product card is rendered for a product with stock quantity greater than 0
- **THEN** no stock banner is rendered on the card

#### Scenario: Screen reader exposure
- **WHEN** a screen reader encounters a card for an out-of-stock product
- **THEN** the out-of-stock text is announced as part of the card's link content, and the signal is not conveyed by color alone

### Requirement: Available quantity on product detail
The product detail page SHALL display the available stock quantity near the price block when stock is greater than zero, using a localized parameterized label. When stock is zero, the quantity line MUST NOT render (the existing out-of-stock badge and disabled Add-to-Cart already communicate unavailability).

#### Scenario: In-stock product shows quantity
- **WHEN** a shopper views a product with stock quantity greater than 0
- **THEN** the detail page shows the localized available-quantity line with the current stock number near the price

#### Scenario: Out-of-stock product hides quantity line
- **WHEN** a shopper views a product with stock quantity 0
- **THEN** no available-quantity line renders, and the existing out-of-stock badge and disabled Add-to-Cart remain

#### Scenario: Bilingual rendering
- **WHEN** the site is viewed in either supported culture (es-AR or en-US)
- **THEN** the available-quantity line renders in the active culture with the number formatted as a plain integer

### Requirement: Structured-data availability matches stock
The product detail page's JSON-LD structured data SHALL advertise `https://schema.org/OutOfStock` availability when stock is zero and `https://schema.org/InStock` otherwise.

#### Scenario: Out-of-stock product metadata
- **WHEN** a product with stock quantity 0 is viewed
- **THEN** the JSON-LD availability value is `https://schema.org/OutOfStock`

#### Scenario: In-stock product metadata
- **WHEN** a product with stock quantity greater than 0 is viewed
- **THEN** the JSON-LD availability value is `https://schema.org/InStock`
