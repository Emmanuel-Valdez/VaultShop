## MODIFIED Requirements

### Requirement: Single mobile storefront navigation

Below the desktop navigation breakpoint, the public/customer storefront SHALL present one fixed bottom navigation with Home, Search, Cart, My account, and Menu actions. It SHALL account for device safe-area insets and reserve page space so it does not obscure content or purchase controls. The active destination SHALL be identifiable without color alone. The desktop navigation SHALL remain available at desktop widths. Admin area pages SHALL use the shared mobile shell and role-aware bottom bar defined by the `admin-hub-navigation` capability instead of the storefront items. Account entry points (login, register, and Manage account) SHALL retain the same mobile shell and navigation affordances as storefront pages so the transition between browsing and account tasks does not change the mobile navigation model.

#### Scenario: Mobile customer navigation
- **WHEN** a shopper visits a customer storefront page below the desktop breakpoint
- **THEN** one bottom bar exposes Home, Search, Cart, My account, and Menu, and no duplicate hamburger navigation controls are shown

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
- **THEN** the shared mobile shell is shown with the admin bottom bar items defined by `admin-hub-navigation`, and the storefront Search, Cart, and My account items are not shown
