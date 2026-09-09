# Design — wholesale-transfer-only

## Context

After `order-email-timing`, company orders are `Pending + DelayedPayment` with no email at creation. Business now needs wholesale transfer-only by default (flag), 50% seña copy, 5-day total deadline (price disclaimer), shipping via WhatsApp, and visual overdue warning. No split-payment model — 50% is copy-only (“no registrable”).

## Decisions

- **Flag name `Payments__CompanyCardPaymentsEnabled`:** aligns with existing `Payments__StripeEnabled`/`BankTransferEnabled`. Default `false` implements new transfer-only rule; per-store via env (vaultshop vs ukiyo differ already).
- **Company emails at creation:** send both user + admin at `CartController.SummaryPOST` when `isCompanyCheckout`. Justified: wholesale verification means few orders, admin must not miss seña. Retail stays 1 mail at creation for BankTransfer to avoid spam.
- **50% copy-only:** no new columns, no `DepositAmount`. `EmailTemplates` branch renders 50%/due date/disclaimers. Real partial tracking deferred to future change if needed.
- **Overdue = visual only:** `DateOnly > PaymentDueDate` adds red class + warning, but `Details_PAY_NOW` and `PaymentStatusService` remain payable. Matches user: “sale en rojo pero aun así permite pagar”.

## Alternatives considered

- Persist 50% as `OrderHeader.DepositDue` column → rejected (YAGNI, few wholesale orders, WhatsApp coordination suffices).
- Block payment when overdue → rejected (user explicitly wants still payable).
- Separate wholesale email template → rejected (reuse `OrderConfirmation` with flag, cheaper).

## Non-Goals

- No DB migration.
- No shipping integration.
- No price auto-update after 5 days (copy only).
