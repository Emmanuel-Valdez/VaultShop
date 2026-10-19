## MODIFIED Requirements

### Requirement: Order summary reflects persisted order data
The order summary (HTML and PDF) SHALL be generated exclusively from persisted order values — header totals (including the frozen shipping charge), line prices/counts, status, payment method/status, customer/company data including the fiscal snapshot, delivery address, delivery modality, and pickup branch snapshot — without recomputing totals.

#### Scenario: Summary matches the persisted order

- **WHEN** a summary is rendered or generated for an order
- **THEN** its items, unit prices, quantities, line totals, grand total, currency context, statuses, and customer/company/fiscal data equal the persisted order values

#### Scenario: Both order kinds render

- **WHEN** a summary is requested for a Customer order and for a Company order
- **THEN** both render with consistent structure; the company one includes the fiscal data snapshot

#### Scenario: Shipping line comes from persisted values

- **WHEN** a summary is rendered for an order whose frozen shipping charge is greater than zero
- **THEN** the summary shows the shipping amount and the delivery modality from the persisted order, and the grand total equals the persisted order total

### Requirement: Transactional emails stay unchanged

The confirmation email flow SHALL keep the same content structure and no summary/PDF attachment is added. This capability allows exactly three deltas: (1) timing per the order lifecycle capability — Stripe/Mercado Pago confirmation and admin alert move to payment approval, while the `BankTransfer` instructions email stays at order creation; (2) the bank-transfer instructions email gains the store WhatsApp number and explicit next-step copy (transfer, then confirm the transfer was sent / send the receipt via WhatsApp); (3) the order confirmation and bank-transfer instructions emails show the frozen shipping charge and delivery modality when the order carries a shipping charge. No fiscal/summary attachment content is added.

#### Scenario: Confirmation email not sent at Stripe/MP order creation

- **WHEN** a customer or company order is created with Stripe/Mercado Pago (`PaymentStatus=Pending`) or Company delayed payment (`Pending + DelayedPayment`)
- **THEN** no order-confirmation or admin new-order alert email has been sent yet

#### Scenario: Bank-transfer instructions email sent at creation

- **WHEN** a `BankTransfer` order is created
- **THEN** the order-confirmation email is sent immediately, includes the CBU/alias transfer instructions and the store WhatsApp number, and states the customer must confirm the transfer was sent or send the receipt via WhatsApp

#### Scenario: Confirmation email sent on payment approval

- **WHEN** a Stripe/Mercado Pago order transitions to `PaymentStatus=Approved` via webhook, browser payment sync, or admin bank-transfer approval
- **THEN** the order-confirmation email (Stripe/MP only) and the admin new-order alert email are sent (idempotently; re-delivery does not duplicate)

#### Scenario: Confirmation email unchanged

- **WHEN** a Stripe/Mercado Pago confirmation email is sent after this change
- **THEN** it has the same content structure as before, with no attached document, and the only permitted content addition is the frozen shipping line when the order carries a shipping charge

#### Scenario: Shipping line rendered when charged

- **WHEN** a confirmation or bank-transfer instructions email is sent for an order whose frozen shipping charge is greater than zero
- **THEN** the email's totals show the shipping amount and the delivery modality alongside the existing line and discount information

#### Scenario: Confirmation email from webhook renders in server default culture

- **WHEN** the confirmation email is triggered by a webhook or system path (no user request culture available)
- **THEN** it renders in the server default culture instead of the shopper's culture; emails triggered from a user request keep that request's culture
