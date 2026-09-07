## Why

`CartController.SummaryPOST` sends both the user order-confirmation and the admin new-order alert immediately after `CheckoutService.CreateOrder`, before any payment is confirmed. Unpaid customer orders look paid to users and spam admins; Company orders are created as `OrderStatus=Approved + PaymentStatus=DelayedPayment`, so they appear fulfilled before payment. This is the follow-up queued on 2026-09-06 (Opción B) to run right after stock-inventory.

## What Changes

- **Customer orders (Stripe / Mercado Pago):** created as `Pending + Pending`. No confirmation or admin alert at creation. Both emails move to payment-confirmation points.
- **Customer orders (BankTransfer):** created as `Pending + Pending`, but the confirmation email is sent **at creation** because it doubles as the payment guide (CBU/alias + store WhatsApp number to send the transfer receipt). Its copy is updated to state explicitly: transfer, then confirm the transfer was sent / send the receipt via WhatsApp. The admin alert for BankTransfer fires only on admin approval.
- **Company orders:** created as `Pending + DelayedPayment` (not `Approved`). `Approved` only after payment is confirmed.
- **Email timing (Opción B):** `TrySendOrderConfirmationAsync` and `TrySendAdminNewOrderAlertAsync` are removed from `SummaryPOST` and triggered only when payment transitions to `Approved`: `PaymentStatusService.MarkCheckoutSessionPaid` (Stripe/MP webhook + browser sync), `PaymentStatusService.ApproveManualBankTransfer` (admin approval for bank transfer), and the Company delayed-payment approval path.
- Idempotency preserved via existing `OrderHeader.OrderConfirmationEmailSentUtc` / `PaymentReceiptEmailSentUtc` guards; no duplicate sends on webhook re-delivery or browser re-visit. The confirmation guard becomes an atomic claim (conditional UPDATE) because webhook + browser sync can now race.
- **Consequence — paid-transition promotion:** Company orders paid while `Pending` must be promoted to `Approved + Approved` (today the paid transition preserves OrderStatus for `DelayedPayment` orders, which would leave them stuck at `Pending + Approved`).
- **Consequence — email language:** confirmation sent from webhook/system paths renders in the server default culture (no user culture available); accepted — es-AR is the primary market. Persisting culture per order is out of scope (would need a new column).
- **Consequence — store WhatsApp:** new `BrandingOptions.WhatsAppNumber` config (env `Branding__WhatsAppNumber` in `docker-compose.example`), shown in the bank-transfer instructions email and available app-wide for future features.

## Capabilities

### New Capabilities
- `order-lifecycle`: order status + email timing (Opción B) — `Approved` only after payment; confirmation and admin alert sent on paid transition.

### Modified Capabilities
- `stock-inventory`: Company checkout scenario now expects `Pending + DelayedPayment` at creation (was `Approved`).
- `billing-invoicing`: confirmation email timing moves from order creation to payment approval; content unchanged.

## Impact

- **Code:** `VaultShop.Web/Areas/Customer/Controllers/CartController.cs` (SummaryPOST conditional email), `VaultShop.Web/Services/Checkout/CheckoutService.cs` (Company initial status), `VaultShop.Web/Services/Payments/PaymentStatusService.cs` (paid-transition promotion + awaited email sends), `VaultShop.Web/Services/Email/TransactionalEmailService.cs` (atomic timestamp claim + WhatsApp passthrough), email templates (WhatsApp + explicit next-step copy), `BrandingOptions` + `docker-compose.example` (`Branding__WhatsAppNumber`), plus await updates at the `MarkCheckoutSessionPaid`/`ApproveManualBankTransfer` callers (`StripeWebhookController`, `MercadoPagoWebhookController`, `CartController.SyncPaidCheckoutSession`, `OrderController`). Webhook logic itself is unchanged.
- **Tests:** `VaultShop.Tests` — checkout creation tests + payment status service tests + email timing tests.
- **No schema migration.** No new dependencies. Behavior change is intentionally visible: OrderConfirmation/Details must reflect Pending until paid.
