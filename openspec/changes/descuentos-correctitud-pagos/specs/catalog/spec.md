## MODIFIED Requirements

### Requirement: Storefront shows sale and promotion badges

Product cards, search results, favorites rows, and product details SHALL show a localized badge when a direct offer or automatic/collection promotion is active for the viewer (e.g. offer, 2x1, collection -20%), plus struck-through original price wherever a discounted price is displayed. A BxGy badge SHALL appear only when the same rule that grants the cart discount considers the promotion grantable for that viewer (active, in window, wholesale opt-in respected, valid quantities, positive benefit) — a badge SHALL never promise a benefit the cart would not grant.

#### Scenario: Offer product shows badge and strikethrough
- **WHEN** a shopper views a listing containing a product with an active direct offer
- **THEN** the card shows the offer badge, the offer price, and the original price struck-through

#### Scenario: BxGy product shows promotion badge
- **WHEN** a product is covered by an active BxGy promotion for the viewer
- **THEN** its card and detail show the promotion badge (e.g. 2x1)

#### Scenario: Inactive or misconfigured promotion shows no badge
- **WHEN** a BxGy promotion is inactive, out of window, or has zero quantities
- **THEN** no badge is rendered even if the product is in scope

#### Scenario: No active discount means no badge
- **WHEN** no offer or promotion is active for a product and viewer
- **THEN** no discount badge or strikethrough is rendered
