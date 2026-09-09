## Why

Stripe/Mercado Pago orders are `Pending` until the provider confirms payment via webhook or browser return. If the webhook is lost (network, signature, deploy) and the customer never revisits the success URL, the order stays `Pending` forever even though the provider reports `paid`. There is no background re-check today; paid orders can be stuck without staff noticing, blocking fulfillment and causing support risk.

## What Changes

- Add a background reconciliation job (`BackgroundService`) that periodically re-checks stale `Pending` orders against the provider APIs (Stripe + Mercado Pago) and promotes them to `Approved/Approved` when the provider reports `paid`.
- Poll only provider-paid methods (`Stripe`, `MercadoPago`); `BankTransfer` is never auto-reconciled — it requires manual admin approval.
- Use the existing `IPaymentSessionService.GetCheckoutSessionStatus` + `IPaymentStatusService.MarkCheckoutSessionPaid` path, so status transitions, idempotency, and email timing (`order-lifecycle` Opción B, plus the payment receipt email both webhooks send) are reused unchanged. For Mercado Pago, the result is validated against `ExternalReference`/`TransactionAmount` before promotion, mirroring the webhook.
- Make the job resilient: scoped DbContext per iteration, per-order try/catch, transient HTTP errors logged and retried next cycle, never blocking checkout/webhooks.
- Add config for interval, staleness window, and batch cap (`Payments:Reconciliation:*`); disabled by default when keys are missing or provider not configured.
- Log structured outcomes per order (reconciled / still pending / skipped / failed); no new UI required for v1.

## Capabilities

### New Capabilities
- `payment-reconciliation`: background re-verification of stale pending provider orders against Stripe/Mercado Pago and idempotent promotion to paid.

### Modified Capabilities
<!-- none — reuses order-lifecycle status/email rules without changing them -->

## Impact

- **Code:** new `VaultShop.Web/Services/Payments/PaymentReconciliationBackgroundService.cs` (or `HostedServices/`), `Program.cs` registration (`AddHostedService`), `appsettings.json` + env examples (`Payments:Reconciliation:Interval`, `StaleAfter`, `MaxAge`, `BatchSize`), optional health/logging tweaks. No DB migration.
- **Dependencies:** none — uses built-in `IHostedService` + existing payment clients (`IStripeCheckoutSessionClient`/`MercadoPagoHttp` via keyed `IPaymentSessionService`).
- **Tests:** unit tests for stale-order selection and `GetCheckoutSessionStatus` → `MarkCheckoutSessionPaid` delegation; integration smoke with fake payment client.
- **Ops:** webhook remains primary path; reconciliation is safety net with low frequency (e.g., every 5–15 min) to avoid provider rate limits. BankTransfer and already-terminal orders are excluded.
