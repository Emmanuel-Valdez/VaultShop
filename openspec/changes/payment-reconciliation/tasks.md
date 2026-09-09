## 1. Config and registration

- [x] 1.1 Add `Payments:Reconciliation` config defaults to `appsettings.json` (`Enabled=false`, `Interval=00:10:00`, `StaleAfter=00:05:00`, `MaxAge=48:00:00`, `BatchSize=20`) and document env overrides in `.env.compose.example`, verify `dotnet build VaultShop.sln --no-restore` clean
- [x] 1.2 Create `PaymentReconciliationOptions` POCO bound to `Payments:Reconciliation` with validation (`StaleAfter < MaxAge`, `BatchSize >=1`, `Interval >= 00:01:00`), verify options bind correctly in `Program.cs` startup logs (no secrets)

## 2. Background reconciliation service

- [x] 2.1 Implement `PaymentReconciliationBackgroundService : BackgroundService` with `PeriodicTimer` at `Options.Interval`, `IServiceScope` per cycle, and early return when `Enabled=false`; before dispatching each order, skip providers disabled via `Payments:StripeEnabled` / `Payments:MercadoPagoEnabled`, verify service starts without errors when disabled (check logs)
- [x] 2.2 Implement stale batch selection: query `Pending` orders where `PaymentMethod in (Stripe, MercadoPago)`, `SessionId != null`, `OrderDate` in `[UtcNow-MaxAge, UtcNow-StaleAfter]`, not terminal (`Cancelled/Refunded/Rejected`), ordered by `OrderDate` asc, `Take(BatchSize)`, verify via unit test with in-memory/fake `IUnitOfWork` that correct orders are selected and BankTransfer/terminal/already-paid/too-fresh/too-old are excluded
- [x] 2.3 For each order, resolve keyed `IPaymentSessionService` by `PaymentMethod`, call `GetCheckoutSessionStatus(sessionId, null)` and on `IsPaid==true` map to `PaymentSessionStatusUpdate(order.Id, sessionId, paymentIntentId)` and call scoped `IPaymentStatusService.MarkCheckoutSessionPaid`; on success call `ITransactionalEmailService.TrySendPaymentReceiptAsync(order.Id)` (same as both webhook controllers); for `MercadoPago` validate `ExternalReference == order.Id` and `TransactionAmount == OrderTotal` on the search result before promoting (mirrors webhook); preserving existing idempotency and email guards, verify unit test: paid Stripe order is promoted and confirmation/admin/receipt emails fire once, unpaid remains pending, duplicate already-paid is no-op, MP amount/reference mismatch stays pending
- [x] 2.4 Add per-order isolation: `try/catch` per order (catch `HttpRequestException`/timeout → `Warning`, other `Exception` → `Warning` with orderId, continue to next), `OperationCanceledException` propagates to respect shutdown, verify unit test that a failing order does not abort the batch and succeeding orders still reconcile
- [x] 2.5 Register service in `Program.cs` via `AddHostedService<PaymentReconciliationBackgroundService>()`, verify `dotnet test VaultShop.sln` green and `dotnet build --no-restore` clean; manual smoke: set `Enabled=true` with fake provider locally and confirm a stale pending order is reconciled without webhook

## 3. Mercado Pago resolution refinement

- [x] 3.1 Verify the MP reconciliation path: `GetCheckoutSessionStatus(sessionId, null)` already searches by `preference_id` (no webhook `paymentId` needed); ensure the reconciliation result validates `ExternalReference == order.Id` and `TransactionAmount == OrderTotal` before promotion, consistent with the webhook path, verify MP unit test: stale `MercadoPago` order paid at provider is reconciled via the preference search, and a mismatched amount/reference leaves the order `Pending` with a warning log

## 4. Observability and hardening

- [x] 4.1 Add structured logs per cycle (cycle start/end, batch size, reconciled count) and per order (`Information` on reconciled, `Debug` on still-pending/skipped, `Warning` on provider failure) without secrets or payload bodies, verify via log assertion test or manual log inspection
- [x] 4.2 Ensure singleton lifetime safety: no captured scoped services, scope created inside `ExecuteAsync`, verify no `InvalidOperationException: Cannot consume scoped service from singleton` at startup

## 5. Tests and verification

- [x] 5.1 Add `PaymentReconciliationTests` covering selection window, BatchSize cap, BankTransfer skip, terminal skip, idempotency, per-order isolation, disabled-provider no-op, MP amount/reference mismatch, and receipt email sent exactly once on promotion, verify `dotnet test VaultShop.sln` green
- [x] 5.2 Manual/sandbox verification: create a real `Stripe` pending order with a valid `SessionId`, stop webhook delivery, enable reconciliation, confirm order promotes to `Approved` after `StaleAfter` and emails fire once; MP verification deferred to provider simulation if sandbox does not emit paid status, document result.
  - Outcome (2026-09-09): Live Stripe/MP sandbox verification skipped because test-mode providers do not emit payment webhooks (confirmed with user). Verified via in-memory fake-provider smoke instead — the same mechanism task 5.2's deferral clause describes: `PaymentReconciliationTests` simulate a paid provider `GetCheckoutSessionStatus` result and confirm a stale `Pending` provider order is promoted through `MarkCheckoutSessionPaid` and the payment-receipt email fires exactly once, with no webhook involved. MP is also covered by the preference-search validation tests. Live sandbox E2E (real `SessionId` + test card) remains a follow-up once production credentials or a webhook-capable sandbox exist.
