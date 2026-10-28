## Purpose

Gives visitors a single light/dark switch in the navigation of every area, following the system preference until they choose a theme.

## ADDED Requirements

### Requirement: Two-state theme toggle

The site SHALL provide a single theme control that switches between light and dark with one activation. The control SHALL NOT expose an "Auto" option. Its accessible name SHALL state the action it performs (for example, "Switch to dark theme") and its pressed or label state SHALL reflect the active theme.

#### Scenario: Switching theme
- **WHEN** a visitor activates the theme control while the light theme is active
- **THEN** the dark theme is applied immediately, the choice is stored, and the control reflects dark

#### Scenario: No Auto option
- **WHEN** a visitor opens any page and inspects the theme control
- **THEN** only a light/dark switch is present and no "Auto" option is shown

### Requirement: Initial theme follows system preference until a choice is stored

When no theme choice is stored, the initial theme SHALL follow the `prefers-color-scheme` setting of the visitor's system. Once the visitor activates the control, that choice SHALL take precedence on later visits. A stored value of `auto` SHALL be treated as no stored choice.

#### Scenario: New visitor with dark system preference
- **WHEN** a visitor with no stored choice and a dark system preference loads any page
- **THEN** the dark theme is applied

#### Scenario: Legacy auto value
- **WHEN** a visitor's stored theme is `auto`
- **THEN** the system preference is used and the page renders without error

### Requirement: Theme control placed in navigation, not floating

The theme control SHALL be part of the navigation of each area and SHALL NOT be rendered as a fixed floating element. On desktop it SHALL appear in the top navigation. On storefront mobile (below the desktop breakpoint) it SHALL appear inside the Menu offcanvas and SHALL NOT appear in the fixed bottom navigation. The control SHALL NOT overlap page content or purchase controls.

#### Scenario: Desktop navigation
- **WHEN** a visitor views any page at or above the desktop breakpoint
- **THEN** the theme control is in the top navigation and no floating control is present

#### Scenario: Storefront mobile menu
- **WHEN** a shopper opens the Menu offcanvas below the desktop breakpoint
- **THEN** the theme control is available inside it and the bottom navigation has no theme control

#### Scenario: No overlap with content
- **WHEN** a shopper scrolls a storefront page on a 390px viewport
- **THEN** no theme control is fixed over product cards, the cart, or checkout actions

### Requirement: Theme control available in every area

The theme control SHALL be available on storefront, account (Identity), and administration pages, using the same two-state behavior in each.

#### Scenario: Account page
- **WHEN** a shopper opens Login or Manage account
- **THEN** the theme control is available in that page's navigation and switches the theme

#### Scenario: Admin page
- **WHEN** an administrator opens an Admin page
- **THEN** the theme control is available in the navigation and switches the theme
