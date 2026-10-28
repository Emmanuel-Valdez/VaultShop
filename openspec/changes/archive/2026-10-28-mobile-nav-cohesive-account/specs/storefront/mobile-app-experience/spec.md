## MODIFIED Requirements

### Requirement: Single mobile storefront navigation

Below the desktop navigation breakpoint, the public/customer storefront SHALL present one fixed bottom navigation with Home, Search, Cart, My account, and Menu actions. It SHALL account for device safe-area insets and reserve page space so it does not obscure content or purchase controls. The active destination SHALL be identifiable without color alone. The desktop navigation SHALL remain available at desktop widths, and the mobile storefront navigation SHALL NOT replace the Admin navigation. Account entry points (login, register, and Manage account) SHALL retain the same mobile shell and navigation affordances as storefront pages so the transition between browsing and account tasks does not change the mobile navigation model.

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

## ADDED Requirements

### Requirement: Visible logout in account navigation

Authenticated shoppers SHALL be able to sign out from the mobile account navigation without locating a desktop-only dropdown. The logout action SHALL be available wherever the mobile "My account" entry point is presented, including on account pages, and SHALL preserve the existing return-to-home behavior.

#### Scenario: Logout available on mobile
- **WHEN** an authenticated shopper opens the mobile account navigation below the desktop breakpoint
- **THEN** a visible Logout action is present and submits the existing logout flow

#### Scenario: Logout on account pages
- **WHEN** an authenticated shopper is on a Manage account page below the desktop breakpoint
- **THEN** the same visible Logout action remains available and returns the shopper to the storefront home after sign-out