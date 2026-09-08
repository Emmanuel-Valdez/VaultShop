## Why

Company (mayorista) orders are `Pending + DelayedPayment` with `PaymentDueDate = OrderDate + 5d` (`SD.CompanyPaymentDueDays`). After `order-email-timing` they stopped sending any email at creation (only at paid transition), so wholesale customers and admins get no instructions. Wholesale also still shows card options and generic copy, while business now requires: transfer-only by default (flag-gated card), 50% seña inmediata para iniciar fabricación/reserva, 5 días para pagar el total (precio puede actualizarse), envío siempre a cargo del comprador coordinado por WhatsApp. The wholesale marketing page still says 7 días and generic dispatch, and `Details` shows a two-line Stripe help text that should be single line. Overdue orders should visibly warn but remain payable.

## What Changes

- **New flag `Payments__CompanyCardPaymentsEnabled` (env `.env.compose`, default `false`):** when `false`, company orders only see/use `BankTransfer`; `Stripe`/`MercadoPago` are hidden in `Details_PAY_NOW` picker and blocked on POST (transfer stays transversal, always enabled via `Payments__BankTransferEnabled`).
- **Company creation email:** `CartController.SummaryPOST` now sends `TrySendOrderConfirmationAsync` (user) at creation for company orders with 50%/5d wholesale copy; admin alert stays at paid transition (`PaymentStatusService`) to avoid duplicate (spec `order-email-timing` says admin alert never at creation). This explains why company emails disappeared: `order-email-timing` moved them to paid transition.
- **Email copy (solo texto, no split):** `EmailTemplates.OrderConfirmation` gains wholesale branch (`isCompanyWholesale` + `paymentDueDate`): seña 50% inmediata, vencimiento `dd/mm` + 5 días, disclaimer precio, envío a cargo coordinado por WhatsApp + CBU/alias/banco/titular + WhatsApp number. `TransactionalEmailService` passes `PaymentDueDate` and `isCompanyWholesale`.
- **Wholesale view:** `Home/Wholesale` resx updated: método pago = transferencia + seña 50% inmediata, plazo = 5 días (seña inmediata, precio puede cambiar), despacho = 7-14 días desde acreditación de seña + envío a cargo por WhatsApp.
- **Cartel fix:** `Order/Details` `canPayNow` alert now shows only `ChoosePaymentMethod` (`Elegí cómo pagar esta orden`), removes `CompanyPaymentMethodChoiceHelp` second line.
- **Overdue visual:** `Order/Details` and `Cart/OrderConfirmation` compute `isOverdue = DelayedPayment && PaymentDueDate != default && today > PaymentDueDate`; due date input gets `border-danger text-danger fw-bold` + small warning (`PaymentOverdueWarning`), order still payable (red but not blocked).
- **Customer confirm:** `canCustomerConfirmTransfer` now allows company users (removed `CompanyId==0` guard) so mayorista can confirm seña/total via `ConfirmTransferSent` (bank transfer awaiting approval), which sends `AdminBankTransferConfirmationRequest` to admin.

## Capabilities

### New Capabilities
- `order-lifecycle`: wholesale transfer-only toggle, overdue visual

### Modified Capabilities
- `billing-invoicing`: wholesale confirmation email gains 50%/5d/WhatsApp conditions
- `ux`: wholesale marketing page and order detail/confirmation wholesale conditions

## Impact

- **Code:** `CartController.cs` (company creation both emails), `EmailTemplates.cs`/`TransactionalEmailService.cs` (wholesale branch), `OrderController.cs` (flag gate + filtered `ViewData` + overdue), `Details.cshtml` (cartel + overdue + allow company confirm), `OrderConfirmation.cshtml` (wholesale conditions), `docker-compose*.yml`/`.env*` (new flag), resx (wholesale + Details + OrderConfirmation).
- **Tests:** `OrderControllerManualPaymentApprovalTests` updated to mock `Payments:CompanyCardPaymentsEnabled`, plus existing 184 tests remain.
- **No schema migration.** No new email types. Price change after 5 days is copy-only.
