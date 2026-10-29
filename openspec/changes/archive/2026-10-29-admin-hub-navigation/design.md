## Context

- `_Layout.cshtml` computes `isStorefront` (Customer area) and `isAccount` (Identity area) and derives `hasMobileShell` from them. The mobile shell (bottom bar, search overlay, `.mobile-shell` padding, hidden top hamburger) is gated on that flag. Admin pages use the same `_Layout` (via `Areas/Admin/Views/_ViewStart.cshtml`) and therefore currently get the desktop top navbar on mobile.
- The bottom bar is hardcoded to five storefront items (Home, Search, Cart, My account, Menu) and `StorefrontMenu` (categories and collections) is rendered only when `isStorefront`.
- The offcanvas already renders Admin links role-gated by `User.IsInRole(SD.Role_Admin)` / `SD.Role_Employee`, and the search form for `!isStorefront`.
- The Admin area has no home or hub controller today. Existing Admin controllers use `[Area("Admin")]` with `[Authorize]` at class level and role-specific attributes on actions.
- Specs are a flat list of top-level capabilities (for example `admin-preview`) and domain folders (`storefront/`, `catalog/`). See proposal.md, Capabilities.

## Goals / Non-Goals

**Goals:**
- One mobile shell for every page of the app.
- Admin and Employee see an admin-specific bottom bar; shoppers see the existing bar unchanged.
- A hub page that groups existing admin destinations without new data queries.

**Non-Goals:**
- Changing the customer bottom bar (cart placement, "My purchases", notifications). Those are separate changes.
- Changing any route, permission, or controller action outside the new hub.
- Desktop navigation changes.
- Dashboard statistics.

## Decisions

**D1. Extend the existing flag instead of a new layout.**
`hasMobileShell = isStorefront || isAccount || isAdmin`, where `isAdmin` is `routeArea == "Admin"`. Alternative: a separate `Admin/_Layout.cshtml`. Rejected: it duplicates the offcanvas, search overlay, and theme toggle, and it is the source of the two-systems confusion this change removes.

**D2. Role decides the bottom bar inside the shell, not a separate shell.**
In `_Layout`, a `isStaff = User.IsInRole(SD.Role_Admin) || User.IsInRole(SD.Role_Employee)` flag selects one of two `<nav>` item sets. The `storefront-mobile-nav` class names are kept so existing selectors and the Theme toggle test keep working. Alternative: a partial `_MobileNav.cshtml` per role. Rejected for now: only one branch differs; a partial would be extracted later if a third role needs its own bar.
Anonymous visitors on Admin URLs are redirected to login before rendering, so `isStaff` is false only for Customer and anonymous pages.

**D3. Hub location: new `HubController` in the Admin area.**
`Areas/Admin/Controllers/HubController.cs`, action `Index`, route `/{culture}/Admin/Hub/Index`, `[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]`. Alternative: put the hub in `SystemController` or `HomeController`. Rejected: `SystemController` is for technical pages (Version); a dedicated controller is the future dashboard entry point.
The view builds its link list from a single in-view model of sections. Role gating for Users, Create user, and Version uses the same `User.IsInRole` checks the navbar uses today, so the rules stay in one known place.

**D4. Admin slot points to the hub; Orders and Products point to their list pages.**
Bottom bar item targets: Home → `Customer/Home/Index`, Orders → `Admin/Order/Index`, Products → `Admin/Product/Index`, Admin → `Admin/Hub/Index`, Menu → offcanvas. Active state uses the current controller name: Admin slot active on `Hub`, Orders active on `Order`, Products active on `Product`.

**D5. Customer Search overlay on admin pages.**
The search overlay is rendered only for the shopper bar. On admin pages it is not needed because the admin bar has no Search item; the offcanvas keeps its existing search form. This removes the storefront search from admin pages.

**D6. Localization in existing `.resx`.**
New keys added to `Resources/Views/Shared/_Layout.{es,en}.resx` for the bar labels and to `Resources/Areas/Admin/Views/Hub/Index.{es,en}.resx` for hub labels, following the naming of existing Admin resources.

## Risks / Trade-offs

- **Admin pages get wider horizontal spacing needs on phones.** → The mobile DataTables density rules already exist (`site.css` admin block). Verify the Order and Product tables at 390px.
- **Shopper tests share helpers with admin.** → `AssertMobileNavItems` is the shopper assertion; add a sibling `AssertAdminNavItems` rather than parameterize.
- **Hub drifts from navbar.** → The hub and the desktop navbar both list links by hand. Mitigation: the hub is the spec source; the navbar keeps its dropdowns in this change. Consolidating them is a follow-up.
- **Employee sees a different link set.** → Hub section gating reuses the exact role checks from the navbar; tests cover Admin and Employee.
- **Rollback.** → Single commit; reverting restores the `hasMobileShell` flag and removes the hub. No data or migration involved.

## Migration Plan

1. Add `HubController` and view, and register resources.
2. Update `_Layout.cshtml` flag and bottom bar branches.
3. Update tests in `StorefrontMenuHttpTests` (rewrite the Admin test; add admin nav item assertions) and add hub access tests.
4. Verify at 390px and 1280px for Admin and Employee, and at 390px for Customer (no change).
5. Deploy as normal; rollback by revert.

## Open Questions

None blocking the specs. The consolidation of desktop navbar dropdowns into the hub is intentionally deferred.
