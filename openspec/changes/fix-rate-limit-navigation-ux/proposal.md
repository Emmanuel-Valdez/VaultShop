## Why

The current global limit of 100 requests per minute per IP blocks normal browsing during rapid navigation, and the login policy counts page GETs as well as credential POSTs. Missing static assets (`site.webmanifest`, source maps) and the error-page re-execution add extra requests, so a shopper reaches `429 Too Many Requests` after ordinary clicks; the 429 also blocks `/Home/Error`, leaving the browser's generic error page instead of a branded explanation. Payment webhooks and the Admin order workflow are exposed to the same blunt global limit.

## What Changes

- Raise and re-scope the global limit to a generous **300 requests per 60 seconds** per client, keyed by authenticated user ID and falling back to the real client IP.
- Apply the stricter login limit only to credential **POST** requests (10 per minute per IP); page GETs and reloads are governed only by the global limit. Keep Identity account lockout as the per-account brute-force defense.
- Add focused policies for sensitive flows: checkout/payment creation, password recovery/contact, cart mutations, and search. Admin requests remain subject to the global limit so order-management screens are protected too.
- Exclude non-user traffic from rate limiting: static files, unmatched routes (404s), the error page, health checks, and payment webhooks (or give webhooks a very high dedicated limit).
- Fix the 429 loop: a rejected request must not recursively consume the error-page path. The error page must always be able to render.
- Add a localized, branded 429 response with `Retry-After`, a clear explanation, and a retry/home action instead of an empty body or the browser's generic error page.
- Add or remove the missing `site.webmanifest` and stop development source-map requests from generating counted 404s.
- Add tests for normal rapid navigation, login GET versus POST, sensitive policies, client partitioning, error-page availability, and the 429 response contract.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `security/rate-limiting`: global limit value and client keying, login policy scope, sensitive-flow policies, exclusions for non-user traffic, Admin coverage, and configurable values.
- `security/status-code-pages`: 429 handling, `Retry-After`, friendly branded copy, and the guarantee that the error page cannot be rate-limited into a loop.

## Impact

- `VaultShop.Web/Program.cs` (limiter configuration, pipeline ordering, client keying, rejection handling).
- `VaultShop.Web/Services/RateLimiting/RateLimiterOptions.cs` and `appsettings.json` (limits and policies).
- `VaultShop.Web/Areas/Identity/Pages/Account/Login.cshtml.cs` (scope the login policy to POST).
- `VaultShop.Web/Areas/Customer/Controllers/HomeController.cs`, `Views/Shared/Error.cshtml`, and error localization resources (429 page).
- `VaultShop.Tests/RateLimitingTests.cs` (rewrite GET-based login tests and add new coverage).
- `VaultShop.Web/Views/Shared/_Layout.cshtml` / `wwwroot` (manifest link or file).
- No new dependencies, endpoints, or database changes.
