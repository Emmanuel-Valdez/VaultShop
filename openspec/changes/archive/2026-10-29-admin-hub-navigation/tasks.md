## 1. Hub page

- [x] 1.1 Add `Areas/Admin/Controllers/HubController.cs` with `Index` restricted to Admin and Employee, and verify an anonymous request redirects to login and a Customer request is denied
- [x] 1.2 Add `Views/Hub/Index.cshtml` with the four sections (Operations, Catalog, Costs, Configuration) and role-gated Users, Create user, and Version links, and verify the Admin HTML contains every link listed in the spec and the Employee HTML omits the admin-only links
- [x] 1.3 Add `es-AR` and `en-US` resource entries for the hub and verify the hub renders Spanish labels with `culture=es-AR`

## 2. Shared shell and role-aware bottom bar

- [x] 2.1 In `Views/Shared/_Layout.cshtml`, change `hasMobileShell` to include `routeArea == "Admin"` and verify an Admin page body contains `class="mobile-shell"` and no hamburger toggler in the top bar below the breakpoint
- [x] 2.2 Add the `isStaff` flag and render the Admin bottom bar items (Home, Orders, Products, Admin, Menu) for Admin and Employee, keeping the five shopper items unchanged for others, and verify with HTTP tests that Admin pages list the five admin items and a shopper page still lists `bi-cart3`
- [x] 2.3 Set the active state for the Admin, Orders, and Products items by controller name and verify `aria-current="page"` appears only on the matching item
- [x] 2.4 Add `es-AR` and `en-US` resource entries for the bar labels and verify the labels switch with the language cookie

## 3. Admin offcanvas and search

- [x] 3.1 Confirm the offcanvas on Admin pages shows the Home link, role-gated admin links, and the search form, and does not render `StorefrontMenu`; verify with an HTTP test on an Admin page and an Employee page
- [x] 3.2 Confirm the storefront search overlay (`#storefrontSearch`) is not rendered on Admin pages; verify with an HTTP test

## 4. Tests

- [x] 4.1 Rewrite `StorefrontMenuHttpTests.Admin_KeepsOperationalNavigationWithoutShopperBar` into an Admin shared-shell test and verify it passes with the new behavior
- [x] 4.2 Add `AssertAdminNavItems` next to `AssertMobileNavItems` (do not parameterize the shopper helper) and verify the Admin test uses it
- [x] 4.3 Verify `ThemeToggleHttpTests.AdminPage_RendersToggleTwice_OneInSideNav` still passes
- [x] 4.4 Run `dotnet build VaultShop.sln` with zero warnings and `dotnet test VaultShop.sln` with all tests passing

## 5. Browser verification

- [x] 5.1 At 390px as Admin, open Order list, Product list, and Hub, and verify the bottom bar does not cover the last table row or the pagination control
- [x] 5.2 At 390px as Employee, verify the same pages and that Users, Create user, and Version are not in the menu
- [x] 5.3 At 1280px as Admin, verify the desktop navbar is shown and the bottom bar is absent
- [x] 5.4 At 390px as a Customer, verify the storefront bar is unchanged

## 6. Specs

- [x] 6.1 After verification, sync the delta specs to `openspec/specs/` (`admin-hub-navigation` new, `storefront/mobile-app-experience` modified) using the sync skill or archive flow
