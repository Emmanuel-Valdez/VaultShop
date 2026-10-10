## Purpose

Gives the store admin a Spanish help page that matches the operations they can actually perform, including offers, promotions, coupons, variants, and collections.

## ADDED Requirements

### Requirement: Spanish admin help matches current operations

The help page SHALL be written in Spanish. It SHALL explain creating and editing categories with slug, image, and average shipping cost, and SHALL NOT say that monthly expectation belongs to the category. It SHALL explain product creation with per-product monthly expectation, stock, store availability, offer price and date window, collections, and variants. It SHALL include separate sections for offers, promotions, coupons, variants, and collections. English help copy is not required.

#### Scenario: Category guidance is current

- **WHEN** an admin opens help in Spanish
- **THEN** the category section mentions slug, image, and average shipping cost, and does not assign monthly expectation to the category

#### Scenario: Product guidance includes the new fields

- **WHEN** an admin opens the product-creation section
- **THEN** it mentions monthly expectation on the product, stock, store availability, the offer window, collections, and variants

#### Scenario: New operation sections exist

- **WHEN** an admin opens help
- **THEN** separate sections explain offers, promotions, coupons, variants, and collections

#### Scenario: English culture still shows Spanish help

- **WHEN** the active culture is `en-US` and the admin opens help
- **THEN** the help body remains Spanish
