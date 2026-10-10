## Purpose

Admin-entered offer, coupon, and promotion windows are Argentina wall time; storage and evaluation are UTC; the storefront deadline displays in Argentina time.

## ADDED Requirements

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
