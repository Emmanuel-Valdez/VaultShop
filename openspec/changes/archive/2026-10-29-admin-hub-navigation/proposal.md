## Why

The admin area renders the desktop-style navbar on mobile, with a hamburger and a long list of dropdowns. Staff switch between two navigation models when moving between the shopper storefront and the admin area, and the admin dropdowns are slow to reach on small screens. The mobile shell (bottom bar + menu) already exists for the storefront and account pages. Extending it to the admin area, with a role-aware bottom bar, gives one navigation model across the app and creates a fast entry point (an admin hub) that later admin features can build on.

## What Changes

- Extend the mobile shell to every Admin area page, so `hasMobileShell` covers the Admin area. The shell is rendered from the shared `_Layout`, so no area-specific layout is added.
- Make the bottom bar role-aware:
  - **Customer (and anonymous shoppers):** `Home | Search | Cart | My account | Menu` (unchanged in this change).
  - **Admin and Employee:** `Home | Orders | Products | Admin | Menu`. The Admin slot opens the new admin hub.
- Add an admin hub page: a single page with grouped cards (Operations, Catalog, Costs, Configuration) that links to the destinations currently listed in the desktop admin navbar. It contains no statistics and no database aggregation queries.
- The mobile Menu lists the same role-gated admin links as today; the category and collection list stays storefront-only.
- The admin area keeps its current desktop navbar on `lg` and wider viewports.
- **BREAKING (UX only):** on mobile, admin pages no longer show the hamburger in the top bar; navigation moves to the bottom bar. No routes, URLs, or permissions change.

Out of scope (separate changes): customer bottom bar changes ("My purchases", cart placement), notifications inbox, orders read-only view, dashboard KPIs.

## Capabilities

### New Capabilities
- `admin-hub-navigation`: the admin hub page, role-aware bottom bar slots for Admin and Employee, and the admin mobile shell behavior.

### Modified Capabilities
- `storefront/mobile-app-experience`: the requirement "Single mobile storefront navigation" currently states that the mobile navigation SHALL NOT replace the Admin navigation and that Admin pages do not use the shell. This change reverses that rule for the Admin area and makes the bottom bar role-aware.

## Impact

- `VaultShop.Web/Views/Shared/_Layout.cshtml`: `hasMobileShell` gains the Admin area; the bottom bar items become role-dependent; the shell applies to admin body classes.
- `VaultShop.Web/Areas/Admin/Controllers/` and `Views/`: new hub action and view (`HomeController` or a new `HubController` in Admin, to be decided in design).
- `VaultShop.Web/Resources/` (`es-AR`, `en-US` `.resx`): new labels for the bottom bar and hub cards.
- `VaultShop.Web/wwwroot/css/site.css`: bottom bar item count is 5 for every role; hub card styles reuse existing classes where possible.
- `VaultShop.Tests/StorefrontMenuHttpTests.cs`: `Admin_KeepsOperationalNavigationWithoutShopperBar` asserts the opposite of the new behavior and must be rewritten; `AssertMobileNavItems` may need a role-aware variant.
- `VaultShop.Tests/ThemeToggleHttpTests.cs`: admin theme-toggle assertions must stay green (the offcanvas placement does not change).
- No database changes, no migrations, no new packages.
