# security/status-code-pages Specification

## Purpose
Replace bare HTTP status responses for 404/500-class errors with branded, localized pages that preserve site navigation and do not leak exception details in production.

## Requirements

### Requirement: Status code errors show a friendly page
The system SHALL register a status code handler that re-executes the shared error page for `404 Not Found`, `429 Too Many Requests`, and `500 Internal Server Error`, returning the original status code with a user-friendly body. The shared error page SHALL remain reachable when a rate limit is active.

#### Scenario: Unknown route shows 404 page
- **WHEN** a visitor requests a route that does not exist
- **THEN** the response status is `404` and the body shows the shared error page with navigation and localized copy

#### Scenario: Rate-limited request shows 429 page
- **WHEN** a client exceeds a configured rate limit
- **THEN** the response status is `429` and the body shows the shared, localized error page instead of an empty body or the browser's generic error page

#### Scenario: Server error shows 500 page
- **WHEN** an unhandled exception occurs during a request
- **THEN** the response status is `500` and the body shows the shared error page without exception details in production

#### Scenario: Error page is not rate-limited
- **WHEN** a rate-limited request is re-executed against the shared error page
- **THEN** the error page is served normally and does not recursively receive another `429`

### Requirement: Error pages preserve brand and localization
The system SHALL render error pages using the same layout, theme, and localization resources as the rest of the site.

#### Scenario: Error page in Spanish
- **WHEN** the request culture is `es-AR` and a `404` occurs
- **THEN** the error page displays Spanish copy and keeps the active theme

### Requirement: Error pages do not leak details
The system SHALL not render stack traces, exception messages, or internal paths on the shared error page outside the development environment.

#### Scenario: Production error page is safe
- **WHEN** a `500` error occurs in a non-development environment
- **THEN** the response body contains only a generic message and support link

### Requirement: Rate-limited responses provide actionable feedback
The system SHALL include a `Retry-After` response header in seconds for every `429` response. The localized error page SHALL explain that too many requests were made, state when the client may retry, and provide a link back to the storefront home or the previous context. The page SHALL be accessible and SHALL NOT start an automatic retry loop.

#### Scenario: Retry-After header present
- **WHEN** a request is rejected with `429`
- **THEN** the response includes `Retry-After` with the number of seconds until the limit resets

#### Scenario: Spanish rate-limit page
- **WHEN** the request culture is `es-AR` and a `429` occurs
- **THEN** the error page displays Spanish copy explaining the wait and offers a way back to the storefront

#### Scenario: English rate-limit page
- **WHEN** the request culture is `en-US` and a `429` occurs
- **THEN** the error page displays English copy explaining the wait and offers a way back to the storefront

#### Scenario: User can retry after the wait
- **WHEN** the client waits for the duration stated in `Retry-After` and requests the page again
- **THEN** the request is processed normally
