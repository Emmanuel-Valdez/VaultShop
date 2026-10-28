## 1. Configuration and policy model

- [ ] 1.1 Extend `RateLimiterOptions` with defaults for the global 300/60 policy and the `Login` (10/60), `Checkout` (10/60), `Recovery` (5/60), `Cart` (60/60), and `Search` (60/60) policies, and mirror them in `appsettings.json`; verify options bind through the existing test factory.
- [ ] 1.2 Confirm the production/local configuration overrides work without code changes and document the Admin decision (Admin uses the global limit, not an exemption) in the configuration comments.

## 2. Client partitioning and exemptions

- [ ] 2.1 Move `UseAuthentication()` before `UseRateLimiter()` in `Program.cs`, keeping `UseAuthorization()` after the limiter, and resolve the global partition key as authenticated user ID with an IP fallback; verify an authenticated request resolves a user key and an anonymous request resolves an IP key.
- [ ] 2.2 Update the global partitioner to return a no-limiter partition for `/Home/Error`, unmatched endpoints, `/health/live`, `/health/ready`, Stripe/MercadoPago webhook paths, static paths, and well-known browser probes; verify a missing static path and `/Home/Error` do not consume the global budget.
- [ ] 2.3 Remove `[EnableRateLimiting("Login")]` from `LoginModel` and classify `POST /Identity/Account/Login` inside the global partitioner with the 10/60 `Login` policy, leaving GETs on the global policy; verify with the login GET/POST tests.

## 3. Sensitive flow policies

- [ ] 3.1 Add named `Checkout`, `Recovery`, `Cart`, and `Search` policies to `AddRateLimiter`, each keyed by user ID with IP fallback where applicable, and return `429` with the configured limits; verify each policy produces the expected threshold behavior.
- [ ] 3.2 Apply the named policies to checkout/payment creation, cart mutation, search, and password-recovery/contact endpoints (controller actions via attributes; Razor Page handlers via the partitioner where attributes are unsupported); verify with targeted HTTP tests.
- [ ] 3.3 Verify payment webhook endpoints are exempt from the limiter and still return their normal validation response (not `429`) when called in a burst.

## 4. Friendly 429 response

- [ ] 4.1 Update `OnRejected` to set `Retry-After` in seconds, store the remaining time for the error view, and log the partition key and policy; verify a rejected response includes the header.
- [ ] 4.2 Update `HomeController.Error` and `Error.cshtml` with a 429 branch that shows a localized explanation, the retry wait, and a home/retry action; verify both `es-AR` and `en-US` copy.
- [ ] 4.3 Verify the status-code re-execution cannot rate-limit `/Home/Error` and that a 429 returns the branded page instead of Chrome's empty error page.

## 5. Missing static requests

- [ ] 5.1 Add a valid `wwwroot/site.webmanifest` (or remove the layout link) and verify the browser request returns `200` instead of `404`.
- [ ] 5.2 Confirm source-map and well-known 404s do not affect rate-limit budgets, and document that production source maps are not counted or are omitted.

## 6. Tests and verification

- [ ] 6.1 Rewrite the login rate-limit tests to assert 20 GETs succeed and the 11th POST is rejected; add coverage for the login page remaining available after a POST rejection.
- [ ] 6.2 Add tests for the global 300 limit, `Retry-After`, branded 429 body, error-page availability, static/well-known exemptions, independent user partitions, Admin inclusion, sensitive policies, and webhook exemption.
- [ ] 6.3 Run `dotnet test VaultShop.sln` and `dotnet build VaultShop.sln` with zero warnings and all tests passing.
- [ ] 6.4 In local Docker, verify rapid navigation across storefront/account/Admin does not produce 429, then force a limit and confirm the branded 429 page and retry behavior; check logs for no `/Home/Error` loop.
