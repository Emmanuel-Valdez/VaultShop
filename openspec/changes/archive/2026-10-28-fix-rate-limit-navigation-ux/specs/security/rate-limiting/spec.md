## MODIFIED Requirements

### Requirement: Global request rate limit is enforced

The system SHALL apply a global per-client rate limit of 300 requests per 60-second window, with a configurable permit count, window, and queue limit. The client key SHALL be the authenticated user identifier when available; otherwise the system SHALL use the real client IP address. Admin requests SHALL be included in the global limit. Requests that exceed the limit SHALL be rejected with HTTP status `429 Too Many Requests`.

#### Scenario: Normal traffic allowed

- **WHEN** a client makes requests within the configured global limit
- **THEN** all requests are processed normally

#### Scenario: Rapid navigation allowed

- **WHEN** an authenticated shopper opens 20 different storefront or account pages within one minute
- **THEN** every page is processed normally and none receives `429 Too Many Requests`

#### Scenario: Excessive global traffic rejected

- **WHEN** a client exceeds the configured global request limit within a window
- **THEN** subsequent requests receive `429 Too Many Requests` until the window resets

#### Scenario: Authenticated clients are partitioned

- **WHEN** one authenticated user exhausts the global limit
- **THEN** another authenticated user using the same IP address is still served normally

#### Scenario: Admin traffic is limited

- **WHEN** an authenticated Admin or Employee user exceeds the global limit while managing orders
- **THEN** Admin requests receive `429 Too Many Requests` instead of bypassing the limiter

### Requirement: Login endpoints have a stricter rate limit

The system SHALL apply a dedicated limit of 10 credential POST submissions per 60-second window per real client IP. Login page GET requests, reloads, and navigation back to the page SHALL NOT consume the login policy. The existing Identity per-account lockout SHALL remain the primary defense against repeated failed credentials and SHALL NOT be replaced by the login rate limit.

#### Scenario: Repeated login page views allowed

- **WHEN** a shopper opens or reloads the login page 20 times within one minute without submitting credentials
- **THEN** every page view is served normally and none receives `429 Too Many Requests`

#### Scenario: Repeated login attempts throttled

- **WHEN** a client submits more than 10 login credential POST requests within one minute
- **THEN** requests beyond the login policy limit receive `429 Too Many Requests`

#### Scenario: Login page remains available after a throttled submission

- **WHEN** a client is throttled after submitting credentials
- **THEN** a subsequent GET of the login page is still served while the POST rejection is active

#### Scenario: Account lockout remains independent

- **WHEN** a user submits the configured number of failed passwords for one account
- **THEN** Identity account lockout applies even if the IP-level login limit has not been reached

### Requirement: Rate limits are configurable without code changes

The system SHALL read the permit count, window size, and queue limit for each policy from configuration so values can differ between local development, testing, and production without code changes.

#### Scenario: Production config overrides defaults

- **WHEN** configuration supplies custom rate-limiting values
- **THEN** those values are used instead of the compiled defaults

#### Scenario: Policy values are independent

- **WHEN** the login policy window is changed in configuration
- **THEN** the global, checkout, and other policy limits remain at their own configured values

## ADDED Requirements

### Requirement: Sensitive flows have dedicated limits

The system SHALL apply dedicated rate limits to sensitive actions independently of the global limit: checkout and payment creation SHALL permit up to 10 requests per minute per authenticated user; password recovery and contact submissions SHALL permit up to 5 requests per minute per client; cart mutations and search SHALL permit up to 60 requests per minute per client. Requests beyond a flow's limit SHALL receive HTTP status `429 Too Many Requests`.

#### Scenario: Checkout burst rejected

- **WHEN** an authenticated user submits more than 10 checkout or payment-creation POST requests within one minute
- **THEN** the excess requests receive `429 Too Many Requests`

#### Scenario: Password recovery burst rejected

- **WHEN** a client submits more than 5 password-recovery or contact requests within one minute
- **THEN** the excess requests receive `429 Too Many Requests`

#### Scenario: Normal cart and search use allowed

- **WHEN** a shopper performs up to 60 cart or search requests within one minute
- **THEN** those requests are processed normally

### Requirement: Non-user traffic is exempt from rate limiting

The system SHALL exclude static files, requests that match no endpoint (`404`), the shared error page, health checks, and payment provider webhooks from the global and sensitive-flow limits. Payment webhook authenticity SHALL continue to be enforced by the existing signature verification and SHALL NOT be weakened by this exemption.

#### Scenario: Missing static asset does not consume a permit

- **WHEN** a browser requests a missing source map, manifest, or well-known path
- **THEN** the request does not reduce the client's rate-limit budget

#### Scenario: Error page is always available

- **WHEN** a client has exhausted a rate limit
- **THEN** the shared error page is still served instead of being rejected with another `429`

#### Scenario: Payment webhook is not throttled

- **WHEN** a payment provider delivers a valid webhook during a burst
- **THEN** the webhook is not rejected by the application rate limiter
