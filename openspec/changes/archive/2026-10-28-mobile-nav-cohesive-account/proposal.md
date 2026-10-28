## Why

The mobile storefront navigation introduced in `2026-10-27-storefront-mobile-app-experience` appears only on `Customer` routes. When a shopper enters "My account" (`Identity` area) or order management (`Admin` area), the bottom bar disappears and the previous top navigation returns, creating an inconsistent mobile experience. The shared `_Layout.cshtml` already supports both behaviors; the difference is driven by an area check, not by a separate layout.

## What Changes

- Make the mobile navigation coherent across the customer journey: storefront browsing and account pages (login, register, "My account") share one mobile shell.
- Add a visible **Logout** action inside the "My account" mobile experience so users do not have to find the account dropdown to sign out.
- Keep the mobile navigation area-aware rather than forcing the shopper bottom bar onto administration/back-office tasks. Admin and order management retain their operational navigation, but account/sign-out controls become consistent.
- Preserve existing desktop navigation, role gates, and authorization.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `storefront/mobile-app-experience`: mobile navigation coherence extends to account pages (login, register, Manage account) and requires a visible logout affordance; admin/back-office keeps its own mobile navigation.

## Impact

- `VaultShop.Web/Views/Shared/_Layout.cshtml` (area-based navigation gating and account/logout controls)
- `VaultShop.Web/Areas/Identity/Pages/Account/Manage/*` (navigation and logout within account pages)
- `VaultShop.Web/wwwroot/css/site.css` (mobile nav spacing for account pages)
- Tests: `VaultShop.Tests/StorefrontMenuHttpTests.cs` and related layout/http tests