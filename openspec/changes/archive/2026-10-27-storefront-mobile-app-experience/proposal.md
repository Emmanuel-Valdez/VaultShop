## Why

The shared storefront currently presents two separate navigation bars on mobile, competing search controls, and a bright white page canvas. A focused, app-like mobile shell and a warmer light palette would make browsing more comfortable and keep navigation clear across VaultShop and UkiyoStudio without requiring a client-side application rewrite.

## What Changes

- Introduce a shared warm light canvas inspired by the portfolio palette while preserving each store's configured brand colors and the existing dark theme.
- Replace the duplicate mobile navigation controls with a fixed, safe-area-aware bottom bar: Home, Search, Cart, My account, and Menu.
- Provide one role-aware menu for catalog categories and collections, and move informational links to the footer.
- Open search from the bottom bar in an accessible full-screen overlay; submitting a query or choosing a catalog destination continues through the existing server-rendered MVC routes.
- Address the applicable storefront audit findings for heading semantics, mobile text size, spacing, product-detail first-screen layout, and filter-row affordance.
- Deliver the work in reviewable implementation stages. Stop after each stage for user review before beginning the next.

## Capabilities

### New Capabilities
- `storefront/mobile-app-experience`: Responsive storefront shell, mobile navigation, accessible search overlay, catalog destinations, and shared light-theme canvas.

### Modified Capabilities

## Impact

- Shared Razor layout, customer storefront views, CSS, and small client-side interactions in `VaultShop.Web`.
- Store theme defaults/configuration for both deployments; brand primary/accent colors remain deployment-specific.
- Existing catalog search routes and query-string semantics are reused; no API, React, database, or payment changes are expected.
- Verification covers both brand configurations, light/dark themes, mobile/tablet/desktop layouts, keyboard operation, and reduced-motion preferences.
