# payment-reconciliation Specification

## Purpose
Ensures provider-paid orders that missed their webhook/browser confirmation are eventually promoted to paid by a background reconciliation job that re-verifies stale pending orders against Stripe and Mercado Pago.

## Requirements

### Requirement: Stale pending provider orders are re-verified

The system SHALL periodically select stale `Pending` orders with a provider payment method (`Stripe`, `MercadoPago`) and re-verify each against the provider API; an order that the provider reports as `paid` SHALL be promoted to `PaymentStatus=Approved` (and `OrderStatus` per `order-lifecycle` rules) via the existing `MarkCheckoutSessionPaid` path.

#### Scenario: Stale Stripe order paid at provider is reconciled
- **WHEN** a `Pending+Pending` order with `PaymentMethod=Stripe` has a `SessionId` and is older than `StaleAfter` but younger than `MaxAge`, and `GetCheckoutSessionStatus` reports `paid`
- **THEN** the order is promoted to `Approved` and the Opción B confirmation and admin alert emails are sent idempotently

#### Scenario: Stale Mercado Pago order paid at provider is reconciled
- **WHEN** a `Pending+Pending` order with `PaymentMethod=MercadoPago` and a `SessionId` is within the staleness window and the provider reports `paid`
- **THEN** the order is promoted via `MarkCheckoutSessionPaid` using the stored `SessionId` and returned `PaymentIntentId`

#### Scenario: Mercado Pago result is validated before promotion
- **WHEN** reconciliation resolves a Mercado Pago payment via the `preference_id` search (no webhook `paymentId` available)
- **THEN** it promotes only if `ExternalReference == order.Id` and `TransactionAmount == OrderTotal`, mirroring the webhook validation; otherwise the order remains `Pending` and the mismatch is logged as a `Warning` for manual review

#### Scenario: Still-unpaid provider order remains pending
- **WHEN** a stale provider order is re-verified and the provider reports not `paid`
- **THEN** the order remains `Pending` and is eligible for re-check in a future cycle until `MaxAge`

#### Scenario: BankTransfer orders are never auto-reconciled
- **WHEN** an order has `PaymentMethod=BankTransfer`
- **THEN** reconciliation skips it regardless of age or status

#### Scenario: Terminal or already-paid orders are skipped
- **WHEN** an order is `Cancelled`/`Refunded`/`Rejected` or already `PaymentStatus=Approved`
- **THEN** reconciliation does not call the provider and does not change status

#### Scenario: Bounded batch per cycle
- **WHEN** more stale orders exist than `BatchSize`
- **THEN** at most `BatchSize` orders are verified in one cycle, the remainder are deferred to the next cycle

### Requirement: Reconciliation reuses the existing paid-transition path

Reconciliation SHALL NOT duplicate status-transition or email logic; it SHALL call the provider status API and then delegate to `IPaymentStatusService.MarkCheckoutSessionPaid`, preserving the same idempotency, `OrderStatus` promotion (including Company `Pending+DelayedPayment` → `Approved+Approved`), and email guards as webhook/browser paths.

#### Scenario: Idempotent on duplicate provider success
- **WHEN** reconciliation promotes an order and a concurrent webhook or prior cycle already promoted it
- **THEN** no duplicate status change occurs and the confirmation email is sent exactly once (atomic claim); the admin alert remains best-effort under a rare concurrent race, same as the existing webhook-vs-browser behavior

#### Scenario: Email timing matches order-lifecycle
- **WHEN** reconciliation promotes a provider order to paid
- **THEN** the user confirmation, admin alert, and payment receipt emails (Customer orders only, per `order-lifecycle`) are sent exactly as the webhook path does — receipt via `ITransactionalEmailService.TrySendPaymentReceiptAsync` after promotion, idempotent via `PaymentReceiptEmailSentUtc` — withheld otherwise

### Requirement: Per-order isolation and retry on transient failure

The system SHALL isolate failures per order: an exception or transient HTTP error for one order SHALL be logged and SHALL NOT abort the cycle; the failed order remains eligible for the next cycle.

#### Scenario: Provider transient error does not block others
- **WHEN** `GetCheckoutSessionStatus` throws `HttpRequestException` or timeout for one order
- **THEN** that order is logged as failed, the cycle continues to the next order, and the failed order is retried next cycle

#### Scenario: Unexpected exception is logged and contained
- **WHEN** an unexpected exception occurs while processing an order
- **THEN** the exception is logged with order id and method, the order is skipped for this cycle, and the background service continues running

### Requirement: Configuration and enablement

Reconciliation SHALL be configurable via `Payments:Reconciliation:Enabled`, `Interval`, `StaleAfter`, `MaxAge`, and `BatchSize` with safe defaults, and SHALL be a no-op when `Enabled=false` or when the required provider credentials are not configured.

#### Scenario: Disabled by configuration
- **WHEN** `Payments:Reconciliation:Enabled` is `false` or absent and defaults to disabled
- **THEN** the background service starts but performs no provider calls

#### Scenario: Interval and window respect defaults
- **WHEN** `Interval`, `StaleAfter`, `MaxAge`, or `BatchSize` are not explicitly configured
- **THEN** the service uses defaults (e.g., `Interval=10m`, `StaleAfter=5m`, `MaxAge=48h`, `BatchSize=20`) and validates that `StaleAfter < MaxAge` and `BatchSize >= 1`

#### Scenario: Provider not configured skips that provider
- **WHEN** Stripe (or Mercado Pago) is disabled via `Payments:StripeEnabled` / `Payments:MercadoPagoEnabled` or its credentials are missing
- **THEN** orders for that provider are skipped with a debug log and no API call is attempted (the skip is decided before dispatch, since an unconfigured MP client throws instead of returning a failed HTTP call)

### Requirement: Observability without secrets

The service SHALL log structured outcomes per cycle and per order at `Information` (reconciled) / `Debug` (skipped/still pending) / `Warning` (transient failure) without emitting secrets, full API payloads, or card data, and SHALL NOT expose a new public endpoint for v1.

#### Scenario: Reconciled order is logged
- **WHEN** an order is successfully promoted by reconciliation
- **THEN** an `Information` log records order id, payment method, and session id; no secret or PII beyond the order id is logged

#### Scenario: Failed verification is logged without secrets
- **WHEN** a provider call fails
- **THEN** a `Warning` log records order id, method, and error type without provider keys or response bodies