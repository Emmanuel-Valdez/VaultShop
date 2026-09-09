## 1. Checkout status fix

- [x] 1.1 Change Company order initial status in `CheckoutService.CreateOrder` from `Approved + DelayedPayment` to `Pending + DelayedPayment`; update `CheckoutServiceTests.CreateOrder_ValidCompanyUser_CreatesApprovedDelayedPaymentOrder` (rename + new expectations) and `CartCheckoutHttpTests` (asserts Company = `Approved + DelayedPayment`, ~line 152)
- [x] 1.2 In `CartController.SummaryPOST`: remove `TrySendOrderConfirmationAsync` + `TrySendAdminNewOrderAlertAsync` for Stripe/Mercado Pago orders; keep `TrySendOrderConfirmationAsync` at creation for `BankTransfer` orders only. Verify: Stripe/MP creation sends nothing, bank-transfer creation sends the instructions email, and no admin alert fires at creation for any method
- [x] 1.3 Fix paid-transition status promotion in `PaymentStatusService`: when `PaymentStatus == DelayedPayment`, `nextOrderStatus` must promote `Pending → Approved` but preserve `InProcess`/`Shipped` (old `Approved + DelayedPayment` orders may already be processing); update the existing test in `PaymentStatusServiceTests` (~lines 34-52) that pins the preserve behavior, and add a case for `Pending + DelayedPayment → Approved + Approved`
- [x] 1.4 Add `WhatsAppNumber` to `BrandingOptions`, add `Branding__WhatsAppNumber` env to `docker-compose.example`, and pass it through `TransactionalEmailService` → `EmailTemplates.OrderConfirmation`
- [x] 1.5 Update the bank-transfer instructions email copy (es-AR and en-US templates) to state explicitly: transfer to the given CBU/alias, then confirm the transfer was sent or send the receipt to the store's WhatsApp number

## 2. Paid-transition email trigger

- [x] 2.1 Make `MarkCheckoutSessionPaid` and `ApproveManualBankTransfer` `async Task<bool>`; inject `ITransactionalEmailService` and, only on the path that actually called `UpdateStatus`+`Save` (NOT the duplicate-approval early return, which also returns `true`), await `TrySendOrderConfirmationAsync` + `TrySendAdminNewOrderAlertAsync` with try/catch logging so webhooks never fail on email error
- [x] 2.2 Update all callers to await: `StripeWebhookController`, `MercadoPagoWebhookController`, `CartController.SyncPaidCheckoutSession`, `OrderController` (both paid-transition call sites)
- [x] 2.3 Make the confirmation-email guard atomic: claim `OrderConfirmationEmailSentUtc` with a conditional update (`WHERE Id = @id AND OrderConfirmationEmailSentUtc IS NULL`) and send only when 1 row was claimed, so concurrent webhook + browser sync cannot both send
- [x] 2.4 Verify Company delayed-payment paid path ends at `Approved + Approved` and triggers both emails (covered by 1.3 + 2.1; Company `Details_PAY_NOW` → `SyncPaidCheckoutSession` or bank-transfer switch routes through the same two methods); note the confirmation call no-ops via the `OrderConfirmationEmailSentUtc` guard when already sent at creation (bank transfer), so the admin alert is the real send there

## 3. Verification & tests

- [x] 3.1 Add/update tests: Company checkout creates `Pending + DelayedPayment`, Customer checkout creates `Pending + Pending`, Stripe/MP creation sends no emails, bank-transfer creation sends the instructions email once (with WhatsApp), `MarkCheckoutSessionPaid` sends both emails once, `ApproveManualBankTransfer` sends both emails once, re-delivery is idempotent, duplicate-approval early return sends nothing, `Pending + DelayedPayment` paid transition results in `Approved + Approved`
- [x] 3.2 Run `dotnet test VaultShop.sln` and `dotnet build --no-restore` clean; manually verify OrderConfirmation/Details copy for Pending orders does not claim approved/paid prematurely — including the admin Order Details view/actions, since Company orders now appear in the "pending" tab
