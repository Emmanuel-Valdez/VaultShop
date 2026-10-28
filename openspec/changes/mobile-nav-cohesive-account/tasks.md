## 1. Mobile shell eligibility

- [x] 1.1 In `VaultShop.Web/Views/Shared/_Layout.cshtml`, add `hasMobileShell = isStorefront || routeArea == "Identity"` and use it only for the bottom bar block (~line 407) and mobile top-navbar hiding (~lines 305, 313). Keep `isStorefront` for storefront-only flags. Add `mobile-shell` to `<body>` when `hasMobileShell` is true; keep `storefront-body` only when `isStorefront` is true. Verify rendered output on Login and Manage account at 390px.
- [x] 1.2 Keep desktop navigation, role gates, and authorized links intact; verify desktop (>= lg breakpoint) still renders the full top navigation on Identity pages and Admin pages.
- [x] 1.3 Confirm Admin pages render the same navigation as before (no shopper bottom bar, no top-navbar change) at 390px and desktop.

## 2. Logout affordance

- [x] 2.1 Add a visible Logout control to the mobile account navigation and to `Areas/Identity/Pages/Account/Manage/_ManageNav`, using the existing form pattern from `_LoginPartial.cshtml:13` with `asp-route-returnUrl` set to Home. Do not change `Logout.cshtml.cs`.
  - Implemented in the `_Layout.cshtml` mobile offcanvas gated to `hasMobileShell` instead of `_LoginPartial`, because `_LoginPartial` is hidden by the mobile shell and changing it would alter Admin mobile navigation (task 1.3).
- [x] 2.2 Verify the rendered logout form includes the antiforgery token (`__RequestVerificationToken`) and submits on mobile; verify sign-out lands on storefront home.
- [x] 2.3 Add HTTP tests in `VaultShop.Tests/StorefrontMenuHttpTests.cs` (or a sibling test class following the same pattern):
  - Login and Manage account (authenticated) render the mobile bottom bar with Home, Search, Cart, My account, Menu.
  - Logout form on Login/Manage account has `returnUrl` pointing to Home.
  - Admin page does not render the shopper bottom bar.
  - Identity page still renders the desktop top navbar.

## 3. Styling and spacing

- [x] 3.1 In `wwwroot/css/site.css`, move the mobile bottom-padding rule from `.storefront-body` (lines ~1597-1599) to `.mobile-shell` and keep `.storefront-body` inheriting it, so Identity pages get the same bottom space. Do not change catalog-only `.storefront-body` rules. Verify `_ManageNav` items and account content are not hidden behind the bar at 390px.

## 4. Verification

- [x] 4.1 Run `dotnet build VaultShop.sln` (expect 0 warnings) and `dotnet test VaultShop.sln` (baseline 829 passing; new tests from 2.3 must pass).
- [x] 4.2 Manual check matrix: pages Login, Register, Manage account, storefront Home; widths 390px (mobile) and 1280px (desktop); both stores (VaultShop and UkiyoStudio); light theme, plus dark theme if the site exposes one. Record any gap as a follow-up, not a blocker, if it is outside the account pages.
  - Follow-up: Login was verified in the browser at 390px (shell, active My account, Menu offcanvas) and 1280px (no bottom bar, desktop nav intact). Register/Manage/storefront Home and the UkiyoStudio deployment were covered by HTTP tests only; a browser check of those pages and the second store remains for deployment QA.
