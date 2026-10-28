## Context

See `proposal.md` - Why. The current limiter is defined in `Program.cs:284-321`: a fixed 60-second window, `GlobalPermitLimit = 100`, `GlobalQueueLimit = 0`, keyed only by `HttpContext.Connection.RemoteIpAddress`. `UseRateLimiter()` runs at `Program.cs:354`, after `UseRouting()` and before `UseAuthentication()`/`UseSession()`. The login page applies `[EnableRateLimiting("Login")]` to the whole `LoginModel` (`Areas/Identity/Pages/Account/Login.cshtml.cs:25`), so GETs consume the 10-per-minute login budget.

`UseStatusCodePagesWithReExecute("/Home/Error", ...)` runs before the limiter (`Program.cs:347`), so every 404 triggers a second pass through the limiter for `/Home/Error`. Missing `site.webmanifest` (`_Layout.cshtml:98`) and development source-map requests produce counted 404s. When the global budget is exhausted, `/Home/Error` is rejected too, producing an empty 429 that browsers render as a generic error page.

The existing Identity lockout is configured at 5 failed attempts / 5 minutes (`appsettings.json:41-44`) and is enabled on the password path (`Login.cshtml.cs:128`). Local Docker requests arrive from the bridge gateway (`::ffff:172.19.0.1`), so an IP-only key is effectively a single bucket for local testing and for all clients behind one proxy.

## Goals / Non-Goals

**Goals:**

- Make ordinary browsing and rapid navigation safe under a generous global limit.
- Keep login POST, checkout/payment, recovery/contact, cart, and search protected with focused limits.
- Keep Admin traffic limited, including order-management screens.
- Ensure 404s, static assets, health checks, the error page, and payment webhooks do not consume user budget or get blocked.
- Return a localized, actionable 429 page with `Retry-After` instead of a browser-generic error.
- Preserve the existing per-account Identity lockout as the brute-force defense for login.

**Non-Goals:**

- Changing authentication, authorization, payment signature verification, or account lockout behavior.
- Adding a new rate-limiting library, Redis, or distributed limiter.
- Implementing a WAF/CDN rate limit (the app-level limit remains the safety net).
- Solving bot management or CAPTCHA.
- Changing the global limit for anonymous clients based on browser fingerprinting.

## Decisions

### 1. Use the existing fixed-window limiter with a 300-request global budget

Set `GlobalPermitLimit = 300`, `GlobalWindowSeconds = 60`, and keep `GlobalQueueLimit = 0`. A human navigating at 5 page views per second for a full minute hits the ceiling; normal browsing and repeated navbar clicks stay well below it. Fixed windows are already implemented, configurable, and easy to reason about. A token-bucket limiter would smooth boundary bursts, but at 300/min the boundary effect is not the user-facing problem; revisit only if monitoring shows boundary abuse.

### 2. Key by authenticated user, fall back to real client IP

Resolve the partition key as `user:{NameIdentifier}` when `User.Identity.IsAuthenticated`, otherwise `ip:{RemoteIpAddress}`. Move `UseAuthentication()` before `UseRateLimiter()` so the claim is populated; keep `UseAuthorization()` after the limiter. This prevents one authenticated shopper from consuming another shopper's budget when both share an IP/NAT. Anonymous traffic remains IP-keyed; in local Docker all anonymous requests share the bridge address, but 300/min is sufficient for a single developer and avoids adding a session/fingerprint key.

The real IP requires the host proxy to send `X-Forwarded-For`; `UseForwardedHeaders()` is already registered. The current `KnownProxies.Clear()`/`KnownIPNetworks.Clear()` trusts all forwarders and must be narrowed to the deployed proxy in production; that is an operational follow-up, not a blocker for local Docker.

### 3. Apply the strict login policy only to credential POSTs

Razor Pages does not support `[EnableRateLimiting]` on handler methods, and the current class-level attribute is why GETs consume the login budget. Remove the class attribute and make the global partitioner recognize `POST /Identity/Account/Login`:

- key: `login-ip:{RemoteIpAddress}`
- policy: existing `Login` values, 10 permits / 60 seconds
- GET, reload, and back-navigation to the login page use only the global policy

Per-account brute-force protection stays with Identity lockout (5 failures / 5 minutes). The user explicitly chose IP + Identity lockout over an email-keyed limiter; this avoids reading form state in the limiter and avoids revealing whether an email exists.

### 4. Exempt non-user traffic and unmatched routes

The global partitioner returns `RateLimitPartition.GetNoLimiter(...)` for:

- `/Home/Error` (so the 429 page cannot be blocked or loop)
- requests with no resolved endpoint (404/static misses)
- `/health/live` and `/health/ready`
- `/api/stripe/webhook` and `/api/mercadopago/webhook` (signature verification remains mandatory)
- static-file paths and well-known browser probes

Named policies remain for application actions. Because unmatched routes do not consume permits, a missing map or manifest request costs nothing. `site.webmanifest` is added to `wwwroot` (or the `<link>` is removed); source maps are not counted either way.

### 5. Sensitive-flow policies

Add named policies configured in `RateLimiterOptions`/`appsettings.json`:

| Policy | Applies to | Key | Limit |
|---|---|---|---|
| `Login` | login POST | IP | 10/min |
| `Checkout` | checkout summary POST and payment-session creation | user, else IP | 10/min |
| `Recovery` | forgot-password, reset-password, contact POSTs | IP | 5/min |
| `Cart` | cart mutation POSTs | user, else IP | 60/min |
| `Search` | storefront search | user, else IP | 60/min |

Controller actions can use `[EnableRateLimiting("...")]`. Razor Page POSTs that cannot take handler attributes (login, recovery) are classified in the global partitioner by path and HTTP method. Admin controllers are not exempt: they use the global 300/min budget. Payment webhooks are exempt as above, not given a named limit, to avoid dropping provider retries.

### 6. Render 429 through the shared error page

`OnRejected` sets `Retry-After` from the lease metadata (seconds, rounded up) and stores the value in `HttpContext.Items`. The status-code re-execution renders `/Home/Error?statusCode=429`; `HomeController.Error` supplies a localized title/message and the retry seconds, and `Error.cshtml` adds a 429 branch with a retry/home action. The error action re-applies `Retry-After` to the final response because the re-execution clears response headers.

The error page is excluded from the limiter, so `429 -> /Home/Error` terminates. No auto-refresh or automatic retry is allowed; the user chooses when to retry. For AJAX clients, the rejection remains a 429 with `Retry-After`; the existing client code can surface it as a toast rather than a full-page navigation.

### 7. Tests assert the corrected contract

Rewrite `RateLimitingTests` because the current tests send GETs to the login page and expect 429, which encodes the bug:

- 20 consecutive login GETs succeed.
- The 11th login POST in a window returns 429.
- A login GET still succeeds while the POST policy is exhausted.
- Global 429 includes `Retry-After`, a localized body, and a non-empty page.
- `/Home/Error` is served while a client is rate-limited.
- Missing static/well-known URLs do not consume the global budget.
- Two authenticated users sharing an IP have independent budgets.
- Checkout/recovery limits return 429 at their thresholds.
- Admin requests still hit the global limit; webhooks do not.

## Risks / Trade-offs

- **Shared IP for anonymous users** → Authenticated users are keyed by user ID; anonymous users behind one NAT share the generous 300/min bucket. Monitor and tune if a large office/school network is expected.
- **Fixed-window boundary burst** → A client can send up to 300 requests near a window boundary and another 300 after the reset. Acceptable at 300/min; use a token bucket later if abuse appears.
- **Exempting unmatched routes** → Attackers can request nonexistent paths without hitting the limiter. These are cheap 404s and should be handled by the proxy/CDN; the alternative (counting them) caused the reported incident.
- **Exempting webhooks** → Webhook endpoints are reachable without the limiter; Stripe/Mercado Pago signature verification and idempotency remain the protection. Never weaken those checks.
- **Admin bulk operations** → Admin uses the same global 300/min. If bulk imports or order edits exceed it, add an Admin-specific policy; do not exempt Admin entirely.
- **Proxy trust** → `KnownProxies.Clear()` currently trusts any forwarded header. Narrow it to the real proxy in production; otherwise an attacker who can reach the app directly could spoof the client key.
- **429 on POST loses the in-flight form** → The page explains the wait and offers a way back; do not auto-resubmit. Checkout/payment actions remain protected and idempotent where already implemented.

## Migration Plan

1. Deploy the configuration and code changes together; no database migration is required.
2. Verify the proxy sends `X-Forwarded-For` and restrict known proxies in the deployment environment.
3. Run the full test suite and the manual Docker checks (rapid navigation, login GET/POST, 429 page, Admin, webhooks).
4. Roll back by reverting the commit and restoring the previous limits; the error-page change is backward compatible.

## Open Questions

None. The policy values and login keying reflect the user's decisions: 300/min global, login POST limited by IP, Identity lockout retained, and Admin included rather than exempt.
