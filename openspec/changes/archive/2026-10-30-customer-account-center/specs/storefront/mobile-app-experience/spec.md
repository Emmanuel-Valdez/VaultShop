## MODIFIED Requirements

### Requirement: Single mobile storefront navigation

Below the desktop navigation breakpoint, the public/customer storefront SHALL present one fixed bottom navigation with Home, Search, Cart, My purchases, and Menu actions for authenticated shoppers. Anonymous shoppers SHALL see Home, Search, Cart, My account, and Menu, with My account leading to the sign-in flow. The active destination SHALL be identifiable without color alone. The bar SHALL account for device safe-area insets and reserve page space so it does not obscure content or purchase controls. The desktop navigation SHALL remain available at desktop widths. Admin area pages SHALL use the shared mobile shell and role-aware bottom bar defined by the `admin-hub-navigation` capability instead of the storefront items. Account entry points (login, register, and Manage account) SHALL retain the same mobile shell and navigation affordances as storefront pages so the transition between browsing and account tasks does not change the mobile navigation model. The "My purchases" destination SHALL use the existing read-only order history route. For authenticated shoppers, the "My account" destination SHALL remain available in the mobile Menu and the header user dropdown.

#### Scenario: Mobile customer navigation
- **WHEN** an authenticated shopper visits a customer storefront page below the desktop breakpoint
- **THEN** one bottom bar exposes Home, Search, Cart, My purchases, and Menu, and My purchases opens the read-only order history

#### Scenario: Anonymous shopper navigation
- **WHEN** an anonymous shopper visits a customer storefront page below the desktop breakpoint
- **THEN** one bottom bar exposes Home, Search, Cart, My account, and Menu, and My account leads to sign in

#### Scenario: My account remains reachable
- **WHEN** an authenticated shopper opens the mobile Menu or the header user dropdown
- **THEN** a My account destination is available

#### Scenario: Bottom bar does not cover content
- **WHEN** a shopper scrolls to the end of a storefront page or uses a checkout action
- **THEN** the last content and interactive controls remain reachable above the fixed bar and device safe area

#### Scenario: Desktop navigation remains
- **WHEN** the viewport is at or above the desktop breakpoint
- **THEN** the existing desktop navigation remains usable and the fixed mobile bar is absent

#### Scenario: Account pages keep the mobile shell
- **WHEN** a shopper opens login, register, or Manage account below the desktop breakpoint
- **THEN** the same mobile shell and navigation affordances remain available and no top-only navigation reappears

#### Scenario: Admin pages use the admin shell
- **WHEN** an Admin or Employee user opens an Admin area page below the desktop breakpoint
- **THEN** the shared mobile shell is shown with the admin bottom bar items defined by `admin-hub-navigation`, and the storefront Search, Cart, and My purchases items are not shown

### Requirement: Visible logout in account navigation

Authenticated shoppers SHALL be able to sign out from the mobile account navigation without locating a desktop-only dropdown. The logout action SHALL be available wherever the "My account" entry point is presented, including the mobile Menu, the header user dropdown, and account pages, and SHALL preserve the existing return-to-home behavior.

#### Scenario: Logout available on mobile
- **WHEN** an authenticated shopper opens the mobile Menu below the desktop breakpoint
- **THEN** a visible Logout action is present and submits the existing logout flow

#### Scenario: Logout in the header dropdown
- **WHEN** an authenticated shopper opens the header user dropdown
- **THEN** a Logout action is present and submits the existing logout flow

#### Scenario: Logout on account pages
- **WHEN** an authenticated shopper is on a Manage account page below the desktop breakpoint
- **THEN** the same visible Logout action remains available and returns the shopper to the storefront home after sign-out
