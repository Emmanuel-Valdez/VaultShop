## Context

`TransactionalEmailService.cs` logs `recipient` at `:271` (`Sent {EmailType} for {OrderId} to {Recipient}`) and `:275` (error with `to {Recipient}`), while health probes are already done — `Program.cs:228,354-363` with `security/health-checks` spec (anonymous, JSON, no secrets). See `proposal.md` — Why. Only email logging hygiene remains.

## Goals / Non-Goals

**Goals:**
- Remove all plaintext email addresses from `ILogger` output in `TransactionalEmailService`.
- Keep one grep-friendly line per attempt (`orderId + emailType + success/failure`).

**Non-Goals:**
- Structured logging (Serilog/JSON), Prometheus metrics, OTel tracing, correlation IDs, health changes, sitemap fix, auto-alerting — explicitly deferred until measured pain (see explore notes).

## Decisions

- **Decision: Drop `recipient` param, keep `orderId + emailType`.** No hash, no truncation. — Alternatives: `SHA256(recipient)` or `a***@domain` — rejected: over-engineering; `orderId` already uniquely identifies the recipient via DB join when debugging. Ponytail: `// ponytail: orderId identifies recipient via OrderHeader — no hash needed unless log-only correlation required`.
- **Decision: Single helper `TrySendEmailAsync` is the only edit point.** Centralized change, all callers covered (confirmation, receipt, failed, shipping, admin alerts). — Alternative: per-method fixes — rejected, duplicates logic.
- **Decision: Do not duplicate health spec.** Reference `security/health-checks` in docs/tests; no new requirement for health secret-free behavior.

## Risks / Trade-offs

- [Risk] Debugging without recipient in logs makes it harder to spot misaddressed emails → Mitigation: query `OrderHeader` + `ApplicationUser` by `orderId`; add hash later if operator feedback demands it (one-line addition).
- [Risk] Tests asserting `to {Recipient}` break → Mitigation: update log assertions to expect PII-free template; no behavior change to email delivery.

## Migration Plan

- Single commit: edit `TransactionalEmailService.cs` logger templates, run `dotnet test`. No migration, no config, no rollback complexity — revert commit if needed.

## Open Questions

- None. Hash-on-demand is the only deferrable choice and defaults to "no hash".
