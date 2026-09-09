## Context

See `proposal.md` — webhook loss + no browser revisit leaves `Pending` provider orders stuck. `PaymentStatusService.MarkCheckoutSessionPaid` already handles status promotion, `OrderStatus` rules (Company `Pending+DelayedPayment` → `Approved+Approved`), idempotency, and Opción B email timing. `IPaymentSessionService.GetCheckoutSessionStatus(sessionId)` already abstracts Stripe (`StripePaymentSessionService` → `IStripeCheckoutSessionClient`) and Mercado Pago (`MercadoPagoPaymentSessionService` → `MercadoPagoHttp`/`HttpClient "MercadoPago"`). No existing `BackgroundService`/`IHostedService` for payments; `Program.cs` registers many scoped services and `AddHttpClient("MercadoPago")`. Constraints: reuse existing paths, no new infra.

## Goals / Non-Goals

**Goals:**
- Safety-net reconciliation for `Stripe`/`MercadoPago` without duplicating transition/email logic.
- Scoped, cancellable `BackgroundService` with bounded batch, per-order isolation, and idempotent promotion.
- Config-driven interval/window/batch with safe no-op when disabled or provider unconfigured.

**Non-Goals:**
- `BankTransfer` auto-reconciliation (remains manual `ApproveManualBankTransfer`).
- New UI, new table/columns, or `OrderHeader` migration.
- Queue system (Hangfire/Azure Queue) or cron outside the app — deferred until measured need.
- Refunding/cancelling unpaid stale orders (separate future capability).
- New public endpoint — webhook + browser paths remain primary.

## Decisions

**`BackgroundService` with `IServiceScope` per cycle + `PeriodicTimer` (ponytail: simplest correct primitive).**
Why: built-in `Microsoft.Extensions.Hosting`, no package, respects `CancellationToken` on shutdown. Alternative `IHostedService` + `Task.Delay` loop — equivalent but `PeriodicTimer` is the modern primitive with correct cancellation semantics. `Timer` + raw thread — rejected.

**One shared service, not two per-provider services.**
Why: single interval/window/batch; per-order dispatches to keyed `IPaymentSessionService` by `PaymentMethod`. Two hosted services would duplicate interval/cancellation/config. MP resolution needs no new helper: `MercadoPagoPaymentSessionService.GetCheckoutSessionStatus(sessionId, null)` already searches payments by `preference_id` when no `providerPaymentId` is given. Because that search returns the most recent payment for the preference (`limit=1`), reconciliation MUST validate `ExternalReference == order.Id` and `TransactionAmount == OrderTotal` on the result before promoting — mirroring `MercadoPagoWebhookController`; a mismatch logs `Warning` and leaves the order `Pending`.
Alternative: add new `IPaymentReconciliationClient` abstraction — rejected, duplicates existing status path.

**Reuse `MarkCheckoutSessionPaid(PaymentSessionStatusUpdate)` via scoped `IPaymentStatusService`.**
Why: single source of truth for `IsTerminal`/`IsPayable`/`SessionMatches`, `UpdateStatus`, and email guards (conditional UPDATE on `OrderConfirmationEmailSentUtc`). After a successful promotion, reconciliation also calls `ITransactionalEmailService.TrySendPaymentReceiptAsync(orderId)` — the same call both webhook controllers make — so reconciled orders receive the identical email set (idempotent via `PaymentReceiptEmailSentUtc`). Alternative: replicate UPDATE logic — rejected, drift risk.

**Staleness window (`StaleAfter` → `MaxAge`) + bounded `BatchSize`.**
Why: avoids hammering provider for orders created seconds ago; avoids infinite retry for months-old abandoned orders. Default `StaleAfter=5m` (webhook has time), `MaxAge=48h`, `Interval=10m`, `BatchSize=20` — all overridable via `Payments:Reconciliation:*`. Ordered by `OrderDate` asc (oldest stale first) with `Take(BatchSize)` to bound DB/API work. Alternative: fixed `CreatedAt > Now-1h` — less tunable.

**Selection query in service, not new repository method (ponytail).**
Why: one LINQ query in the hosted service using `IUnitOfWork.OrderHeader.GetAll(...)` or direct `ApplicationDbContext` via scope. Adding `GetStalePendingOrders` to `IRepository` is okay but not required for v1; can be extracted if reused. Prefer the smallest diff; a private helper `GetStaleBatchAsync` inside the service is enough.

**Per-order `try/catch`, continuation on failure.**
Why: one bad order / transient `HttpRequestException` must not kill the cycle. Logged as `Warning`; next cycle retries. `OperationCanceledException` bubbles to respect shutdown.

**Logging only, no new health check.**
Why: `StorageHealthCheck` already exists; reconciliation observability is logs first. Promotion failures are visible; a dedicated `ReconciliationHealthCheck` can be added later if needed.

## Risks / Trade-offs

- **Provider rate limits (429) / cost → Mitigation:** bounded `BatchSize`, `Interval >= 5m`, per-order delay not on critical path; on `429`, log `Warning` and defer to next cycle; no tight loop.
- **MP search-by-preference returns most-recent payment → Mitigation:** mandatory `ExternalReference`/`TransactionAmount` validation before promotion (mirrors webhook); mismatch stays `Pending` with a `Warning`.
- **Provider not configured → Mitigation:** pre-check the existing `Payments:StripeEnabled` / `Payments:MercadoPagoEnabled` flags before dispatch (same flags checkout uses); an unconfigured MP client throws `InvalidOperationException` at call time, so the skip must happen before the keyed-service call.
- **Race with concurrent webhook/browser → Mitigation:** `MarkCheckoutSessionPaid` handles idempotent status; confirmation email uses an atomic claim so only one sender wins. The admin alert is best-effort (no claim column) — a rare webhook-vs-reconciliation race may duplicate it, same trade-off already accepted for webhook-vs-browser.
- **Duplicate charge edge (`PaymentIntentId` mismatch on already-approved order) → Mitigation:** service logs `Error` path already in webhook; reconciliation does not refund — surfaces for manual review.
- **DbContext lifetime in singleton `BackgroundService` → Mitigation:** create `IServiceScope` per cycle; resolve `IUnitOfWork`/`IPaymentStatusService`/keyed `IPaymentSessionService` inside scope.
- **Clock skew / time zones → Mitigation:** compare `OrderDate` in UTC `DateTime.UtcNow` with `StaleAfter`/`MaxAge` as `TimeSpan`.
- **Secrets in logs → Mitigation:** log only `orderId`, `PaymentMethod`, `SessionId`; never log tokens, keys, or full provider payloads.

## Migration Plan

- No migration. Code-only addition: new `PaymentReconciliationBackgroundService.cs` (e.g., `VaultShop.Web/Services/Payments/` or `VaultShop.Web/HostedServices/`), `Program.cs` `AddHostedService<…>()`, `appsettings.json` `Payments:Reconciliation` defaults, `.env.compose.example` doc.
- Deploy: existing orders automatically eligible next cycle; no backfill.
- Rollback: set `Payments:Reconciliation:Enabled=false` or revert commit; primary webhook path unaffected.

## Assumptions

- **Single app instance per deployment.** Docker Compose runs one `web` container per store today, so no two reconcilers compete. If multi-instance scaling (or an overlapping blue/green deploy) is ever introduced, add row locking (`SELECT ... FOR UPDATE SKIP LOCKED`) or a PostgreSQL advisory lock around batch selection — deferred until that need is real.

## Open Questions

- Desired production `Interval`/`BatchSize` tuning under real order volume — defaults are safe; adjust via env without code change.
