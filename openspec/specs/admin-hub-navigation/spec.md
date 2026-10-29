# admin-hub-navigation Specification

## Purpose

Gives Admin and Employee users one mobile navigation shell across the Admin area, with a role-aware bottom bar and a fast admin hub that groups the destinations currently listed in the desktop admin navbar. Creates the entry point for future admin overview features without adding statistics to this capability.

## Requirements

### Requirement: Shared mobile shell in the Admin area

Below the desktop navigation breakpoint, every Admin area page SHALL render the same mobile shell used by the storefront and account pages: one fixed bottom navigation bar, the mobile menu offcanvas, and the reserved bottom page space. The top navigation SHALL NOT show a hamburger control on these pages below the breakpoint. At and above the desktop breakpoint, the existing desktop navigation SHALL remain and the fixed bottom bar SHALL be absent.

#### Scenario: Admin page on a phone
- **WHEN** an Admin or Employee user opens any Admin area page below the desktop breakpoint
- **THEN** the fixed bottom bar is shown, no hamburger control is shown in the top bar, and the last content of the page remains reachable above the bar

#### Scenario: Admin page on desktop
- **WHEN** an Admin or Employee user opens an Admin area page at or above the desktop breakpoint
- **THEN** the desktop navigation is shown and the fixed bottom bar is absent

### Requirement: Role-aware bottom bar

The bottom bar SHALL show destinations according to the signed-in role. Admin and Employee users SHALL see five items in this order: Home, Orders, Products, Admin, Menu. The Home item SHALL link to the storefront home. The Orders item SHALL link to the admin order list. The Products item SHALL link to the admin product list. The Admin item SHALL link to the admin hub. The Menu item SHALL open the mobile menu offcanvas. Shoppers (Customer role and anonymous visitors) SHALL keep the existing bottom bar items unchanged by this capability.

#### Scenario: Admin bottom bar items
- **WHEN** an Admin or Employee user views a page below the desktop breakpoint
- **THEN** the bottom bar shows Home, Orders, Products, Admin, and Menu, in that order, and does not show Search, Cart, or My account

#### Scenario: Shopper bottom bar unchanged
- **WHEN** a shopper or anonymous visitor views a storefront page below the desktop breakpoint
- **THEN** the bottom bar shows the same items as before this change

#### Scenario: Active destination
- **WHEN** an Admin or Employee user is on the admin hub
- **THEN** the Admin item is marked as the current page using both a visual indicator and `aria-current="page"`

### Requirement: Admin hub page

The system SHALL provide an admin hub page that groups administrative destinations into the sections Operations, Catalog, Costs, and Configuration. Each destination SHALL be a link to an existing admin route. The hub SHALL NOT display statistics, totals, or any data aggregation.

Sections and their links SHALL match the destinations of the desktop admin navbar that are role-gated as today:
- Operations: Orders
- Catalog: Categories, Products, Keywords, Banners, Coupons, Promotions, Company
- Costs: Packaging, Packaging by category, Fabric, Fabric by product, Garment hardware, Garment hardware by product, Fixed cost, Retail percentage cost, Wholesale percentage cost, Cost by product, Final price
- Configuration: Users, Create user, Version (Admin only)

#### Scenario: Hub lists the role's destinations
- **WHEN** an Admin user opens the admin hub
- **THEN** every destination in the four sections is shown as a link to its existing admin route

#### Scenario: Employee does not see admin-only links
- **WHEN** an Employee user opens the admin hub
- **THEN** the Users, Create user, and Version links are not rendered

#### Scenario: Hub contains no statistics
- **WHEN** any user opens the admin hub
- **THEN** the page contains no counts, totals, or revenue figures

### Requirement: Admin hub access control

The admin hub SHALL be available only to Admin and Employee roles. A Customer or anonymous user requesting the hub SHALL be denied access using the same mechanism as other Admin area pages.

#### Scenario: Customer requests the hub
- **WHEN** a Customer user requests the admin hub URL
- **THEN** access is denied exactly as for other Admin area pages

### Requirement: Menu keeps role-gated admin links and storefront-only catalog

The mobile Menu on Admin area pages SHALL show the same role-gated admin links as the desktop navbar. It SHALL NOT show the storefront category and collection list on Admin area pages.

#### Scenario: Admin menu on an admin page
- **WHEN** an Admin or Employee user opens the Menu on an Admin area page
- **THEN** the role-gated admin links are shown and no storefront category or collection list is shown

### Requirement: Localized admin navigation labels

All bottom bar labels, the admin hub title, section headings, and card labels SHALL be available in es-AR and en-US and SHALL switch with the active language.

#### Scenario: Language switch on admin hub
- **WHEN** a user with the Admin role switches the language to es-AR on the admin hub
- **THEN** the bottom bar and hub labels are shown in Spanish
