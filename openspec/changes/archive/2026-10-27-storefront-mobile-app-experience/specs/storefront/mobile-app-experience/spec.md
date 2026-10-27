## Purpose

Defines a comfortable, app-like mobile browsing experience for the shared storefront while retaining its server-rendered URLs, per-store brand identity, and existing desktop behavior.

## ADDED Requirements

### Requirement: Warm shared light-theme canvas

In the light theme, the public storefront SHALL use the warm cream canvas `#f6f3ea` and warm light surfaces, while preserving the configured primary and accent colors of each store. The dark theme SHALL retain its existing dark surfaces and colors.

#### Scenario: Light theme on either store
- **WHEN** a shopper selects the light theme on VaultShop or UkiyoStudio
- **THEN** the page canvas is warm cream, readable text and surfaces remain distinguishable, and the store's configured brand colors remain in use

#### Scenario: Existing dark theme
- **WHEN** a shopper selects the dark theme
- **THEN** the cream canvas is not applied and the dark palette remains legible

### Requirement: Single mobile storefront navigation

Below the desktop navigation breakpoint, the public/customer storefront SHALL present one fixed bottom navigation with Home, Search, Cart, My account, and Menu actions. It SHALL account for device safe-area insets and reserve page space so it does not obscure content or purchase controls. The active destination SHALL be identifiable without color alone. The desktop navigation SHALL remain available at desktop widths, and the mobile storefront navigation SHALL NOT replace the Admin navigation.

#### Scenario: Mobile customer navigation
- **WHEN** a shopper visits a customer storefront page below the desktop breakpoint
- **THEN** one bottom bar exposes Home, Search, Cart, My account, and Menu, and no duplicate hamburger navigation controls are shown

#### Scenario: Bottom bar does not cover content
- **WHEN** a shopper scrolls to the end of a storefront page or uses a checkout action
- **THEN** the last content and interactive controls remain reachable above the fixed bar and device safe area

#### Scenario: Desktop navigation remains
- **WHEN** the viewport is at or above the desktop breakpoint
- **THEN** the existing desktop navigation remains usable and the fixed mobile bar is absent

### Requirement: One role-aware menu contains catalog destinations

The mobile Menu action SHALL open one menu containing links to storefront-visible categories and collections. Each destination SHALL use the existing category or collection filter route and preserve its canonical identifier/slug behavior. Informational links such as FAQs, About, Care, Privacy, and Terms SHALL be available in the site footer rather than occupying a second navigation bar. Role-restricted account and administration links SHALL remain restricted to their existing roles.

#### Scenario: Menu lists available categories and collections
- **WHEN** the mobile menu opens
- **THEN** it lists only categories and collections represented by products currently visible and sellable in the storefront, grouped by type

#### Scenario: Catalog destination uses existing filters
- **WHEN** a shopper selects a category or collection from the menu
- **THEN** the browser navigates to the existing Search route with the correct category or keyword identifier and canonical slug where available

#### Scenario: Informational links are in the footer
- **WHEN** a shopper reaches the end of a storefront page
- **THEN** the informational pages are available from the footer and are no longer duplicated in a second mobile navigation bar

#### Scenario: Restricted links remain protected
- **WHEN** a shopper without an administrative role opens the menu
- **THEN** administration-only links are not rendered; users with existing authorized roles retain their applicable links

### Requirement: Search opens as an accessible full-screen overlay

The mobile Search action SHALL open a full-screen search overlay with a labeled search field and a visible close control. Opening and closing the overlay SHALL not navigate or reload the page. Submitting a non-empty search SHALL use the existing server-rendered Search route and GET query semantics. The overlay SHALL support keyboard use, Escape to close, visible focus, and restoration of focus to the Search action when closed.

#### Scenario: Open and close search without navigation
- **WHEN** a shopper activates Search and then closes the overlay
- **THEN** the overlay opens and closes without changing the current URL, and focus returns to the Search action

#### Scenario: Submit a search query
- **WHEN** a shopper submits a non-empty query from the overlay
- **THEN** the existing Search route receives the query as `searchString` and renders the matching server-side results

#### Scenario: Keyboard operation
- **WHEN** a shopper uses the overlay with a keyboard
- **THEN** the search field and close control are reachable in a logical order, Escape closes the overlay, and focus remains visible

### Requirement: Storefront audit fixes preserve readable, accessible browsing

The Home and Login pages SHALL expose a meaningful page-level `h1`. Public storefront text, gutters, and horizontal filter rows SHALL remain readable and usable at narrow viewports. On mobile and tablet product detail pages, the product heading SHALL appear near the beginning of the content rather than only after an unnecessarily tall image. New or changed transitions SHALL respect `prefers-reduced-motion`.

#### Scenario: Home and Login heading semantics
- **WHEN** a shopper views Home or Login
- **THEN** each page exposes one meaningful `h1` in the document outline

#### Scenario: Narrow viewport readability
- **WHEN** a shopper views storefront sections or filter rows on a narrow viewport
- **THEN** body text and gutters are readable, and horizontal overflow is either clearly indicated or replaced with a usable wrapped layout

#### Scenario: Product detail heading is visible early
- **WHEN** a shopper opens a product detail page on a mobile or tablet viewport
- **THEN** the product heading is presented near the beginning of the page without requiring a long scroll past an oversized image

#### Scenario: Reduced-motion preference
- **WHEN** the operating system requests reduced motion
- **THEN** new or changed navigation and search transitions do not require motion to understand or operate
