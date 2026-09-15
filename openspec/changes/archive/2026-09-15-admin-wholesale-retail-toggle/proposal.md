## Why

Admins, employees, and company customers suffer from fragmented price visibility. Company customers only see wholesale pricing on individual `Details` pages, while the rest of the store (Home, Search, Favorites) hides it. Simultaneously, admins/employees lack a way to verify what different customers see.

## What Changes

- Add a UI toggle in the navigation header allowing admins/employees to switch between **Retail Preview** and **Wholesale Preview** storefront modes.
- Unify storefront pricing visibility: consistent price display (Retail vs. Wholesale) across all storefront views (Home, Search, Favorites, Details, Cart, Summary) based on user role (`Role_Company`) **or** the active admin preview mode.
- Store the preview preference in the session.
- Display a prominent indicator banner for admins in preview mode.

## Capabilities

### New Capabilities
- `admin-preview`: Admin/employee toggle for previewing storefront as retail or wholesale customer.

### Modified Capabilities
- `pricing`: Product display and cart pricing rules now respect Role-based rules (for Company users) AND Admin Preview Mode.

## Impact

- **Session / Service**: Unified pricing resolution helper (`PricingHelper.GetPriceForUser`) that checks `Role_Company` OR `AdminPreviewMode`.
- **Controllers / Services**: Price resolution update in product details, `CartController`, and `CheckoutService`.
- **Views**: 
    - Update Home, Search, Favorites, and Details views to use the new unified `PricingHelper`.
    - Add admin toggle buttons to navigation bar (`_Layout.cshtml`).
    - Add active preview mode indicator banner.
- **Tests**: Comprehensive unit/integration tests for unified pricing visibility.
