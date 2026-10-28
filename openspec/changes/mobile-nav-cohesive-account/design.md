## Context

See `proposal.md` for motivation. The shared `VaultShop.Web/Views/Shared/_Layout.cshtml` derives `isStorefront` from `routeArea == "Customer"` (line 26). That single flag also drives the `storefront-body` class (line 126), the desktop top-navbar hiding (lines 305, 313), the `!isStorefront` branches (lines 133, 151, 264), and the mobile bottom bar block (line 407). Identity pages (`Areas/Identity/Pages/_ViewStart.cshtml`) and Admin pages (`Areas/Admin/Views/_ViewStart.cshtml`) use the same layout. The account menu partial is `Views/Shared/_LoginPartial.cshtml`, whose logout form (line 13) posts to `Identity/Account/Logout` with `returnUrl` set to Home. Manage account pages nest `Areas/Identity/Pages/Account/Manage/_Layout.cshtml`, which renders `_ManageNav` in a `col-md-3` column above the content on mobile.

## Goals / Non-Goals

**Goals:**
- Extend the mobile shell to account pages (login, register, Manage account) without duplicating layouts.
- Provide a visible Logout action in the mobile account navigation, including Manage account.
- Keep Admin/order-management navigation independent and operational.

**Non-Goals:**
- Replacing Admin navigation with the shopper bottom bar.
- Adding new dependencies, endpoints, or restructuring MVC routing.
- Changing authorization or role checks.
- Changing `Logout.OnPost` behavior.

## Decisions

1. **Separate mobile-shell flag, not a broader `isStorefront`.** Introduce `var hasMobileShell = isStorefront || routeArea == "Identity";` in `_Layout.cshtml`. Use `hasMobileShell` only for the bottom bar block (line 407) and the mobile top-navbar hiding (lines 305, 313). Keep `isStorefront` for the storefront-only flags (`isHome`, `isSearch`, `isCart`, `!isStorefront` branches) so account pages do not pick up storefront-specific behavior. One shared layout is kept; no area-specific layout files are added. Body classes are split by responsibility, not reused:
   - Add `mobile-shell` to `<body>` when `hasMobileShell` is true. It carries only the bottom-bar space rule, moved from `.storefront-body` (`site.css:1597-1599`) to `.mobile-shell`.
   - Keep `storefront-body` only when `isStorefront` is true. It keeps the catalog component styles (`site.css:1734-1795`: product cards, collection chips, badges, footer links), which Identity pages do not use.
   - The `.storefront-body .bd-mode-toggle` override (`site.css:1601-1604`) is removed by `theme-toggle-navbar`, not here.

2. **Visible logout reuses the existing form pattern.** Add the same form used in `_LoginPartial.cshtml:13` (`asp-area="Identity" asp-page="/Account/Logout" asp-route-returnUrl="<Home>"`) to the mobile menu offcanvas (`#sideNavbarTop`, gated to `hasMobileShell` and authenticated users) and to `_ManageNav`. `_LoginPartial` itself stays unchanged because it is hidden by the mobile shell and is still used by Admin mobile navigation. `Logout.OnPost` only redirects to `returnUrl` when it is provided; without it, `RedirectToPage()` targets the Logout page's GET, which has no handler. Passing `returnUrl` is therefore required for the "return to storefront home" scenario. No new endpoint.

3. **Account pages keep the five shopper actions.** Home/Search/Cart/My account/Menu remain available on login, register, and Manage account so users can leave the account flow. The active "My account" state stays identifiable.

4. **Manage account mobile layout.** On mobile, `_ManageNav` stacks above the account content and the fixed bottom bar can cover its last items. Mitigation: the `.mobile-shell` bottom padding (same rule as storefront, shared through the class) applies on Identity pages, and keep the Logout entry inside `_ManageNav` so it is reachable without scrolling past the bar. No change to `Manage/_Layout.cshtml` structure.

5. **Admin unchanged.** Admin keeps its current responsive top navigation and role-gated menu. If mobile admin usage becomes frequent, handle it as a separate change.

## Risks / Trade-offs

- [Account pages may feel cluttered with shopper actions] → Keep the bottom bar compact and visually consistent with storefront; review at 390px.
- [Bottom bar covers `_ManageNav` items on mobile] → Bottom padding on Identity pages; verify last nav item is reachable at 390px.
- [Logout without `returnUrl` lands on a non-GET page] → Always pass `returnUrl` to Home; covered by an HTTP test (task 2.3).
- [Logout form needs antiforgery token] → The tag-helper form includes the token; verify in rendered HTML (task 2.2).
- [Shared flag changes leak into Admin] → Use a separate `hasMobileShell` flag and test Admin output (task 2.3).

## Migration Plan

Implement in one reviewable pass: add the `hasMobileShell` flag, add logout affordances, adjust spacing CSS, add HTTP tests, then run build/tests and manual mobile checks. Rollback is reverting the commit.

## Open Questions

None. Logout redirect behavior was verified in `Logout.cshtml.cs:27-40` and `_LoginPartial.cshtml:13`.
