## Why

TransactionalEmailService logs recipient email addresses in plaintext at `TransactionalEmailService.cs:271,275` (`to {Recipient}`), making application logs a store of PII. This creates GDPR/privacy exposure when logs are viewed via `docker logs`, persisted to disk, or forwarded to external collectors. The Verified Gaps Backlog (Phase 11, item 4) and `core.md` flag this as the last open half of "Observability basics" — health checks are already shipped and specced.

## What Changes

- Remove plaintext `recipient` / `AdminEmail` from all `ILogger` calls in `VaultShop.Web/Services/Email/TransactionalEmailService.cs` (lines 271, 275 and any other `to {Recipient}` occurrence).
- Replace with PII-free pattern: `orderId + emailType + result` (and optional hashed recipient if correlation needed). No email content or address is ever written to logs.
- No change to email sending behavior, templates, or health endpoints.
- Document that `security/health-checks` already covers secret-free health responses; this change does not modify health.

## Capabilities

### New Capabilities
- `observability/email-logging`: PII-free logging contract for transactional email send attempts (what may and may not appear in logs).

### Modified Capabilities
- (none — `security/health-checks` is referenced but not modified)

## Impact

- Affected code: `VaultShop.Web/Services/Email/TransactionalEmailService.cs`, `VaultShop.Tests` (log assertion updates if any).
- No DB migration, no config change, no new dependency.
- Risk if not done: continued PII in logs; elevated exposure on log forwarding or support ticket sharing.
