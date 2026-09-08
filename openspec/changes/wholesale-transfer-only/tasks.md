## 1. Wholesale transfer-only flag
- [x] 1.1 Add `Payments__CompanyCardPaymentsEnabled` to `VaultShop.Web/.env.example`, `.env.compose.example`, `ukiyo.env.compose.example`, `docker-compose.yml`, `docker-compose.store.yml` (default false)
- [x] 1.2 Gate `OrderController.Details_PAY_NOW` card methods when flag false for company orders; filter `PopulateBankTransferViewData` Stripe/MercadoPago when `isCompanyOrder`
- [x] 1.3 Update tests to mock `CompanyCardPaymentsEnabled` and keep 184 green

## 2. Company creation emails (fix missing emails)
- [x] 2.1 Verify why company emails stopped: `CartController.SummaryPOST:189` only BankTransfer sent confirmation at creation; company `PaymentMethod=null` so none → moved to `PaymentStatusService` paid transition
- [x] 2.2 In `CartController.SummaryPOST` send both `TrySendOrderConfirmationAsync` + `TrySendAdminNewOrderAlertAsync` at creation when `isCompanyCheckout`; keep retail BankTransfer user-only

## 3. Email copy (solo texto, seña 50%)
- [x] 3.1 Extend `EmailTemplates.OrderConfirmation` with `paymentDueDate?` + `isCompanyWholesale` → wholesaleHtml: 50% seña inmediata, vencimiento 5d (date), precio puede cambiar, envío a cargo WhatsApp, bank CBU/alias
- [x] 3.2 Pass `PaymentDueDate` + `isCompanyWholesale` from `TransactionalEmailService.TrySendOrderConfirmationAsync`
- [x] 3.3 Update `Home/Wholesale` resx es/en: PaymentMethodText (seña 50%), PaymentDeadlineText (5 días, seña inmediata, precio), DispatchTimesText (desde seña + envío WhatsApp)

## 4. Views
- [x] 4.1 Fix `Order/Details` cartel: remove `CompanyPaymentMethodChoiceHelp` second line, keep only `ChoosePaymentMethod` (`Elegí cómo pagar esta orden`)
- [x] 4.2 `Order/Details` overdue: `isOverdue` when `DelayedPayment && dueDate < today`; red border + `PaymentOverdueWarning` but still payable
- [x] 4.3 Allow company to use `ConfirmTransferSent`: remove `CompanyId==0` from `canCustomerConfirmTransfer`
- [x] 4.4 `Cart/OrderConfirmation` wholesale block: `isCompanyWholesale` + `isOverdue` → show `WholesaleConditionsTitle/Body`, due date, disclaimers, bank details, overdue badge

## 5. Verification
- [x] 5.1 `dotnet build --no-restore` clean, `dotnet test` 184 passed
- [x] 5.2 Manual: company order creation → 2 mails (user with 50%/5d + CBU, admin alert), details shows single-line cartel, wholesale page 5 días, overdue red but payable, `ConfirmTransferSent` visible for company after choosing BankTransfer
