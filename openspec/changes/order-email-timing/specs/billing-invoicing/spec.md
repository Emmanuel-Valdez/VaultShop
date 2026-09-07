## MODIFIED Requirements

### Requirement: Transactional emails stay unchanged

The confirmation email flow SHALL keep the same content structure and no summary/PDF attachment is added. This capability allows exactly two deltas: (1) timing per the order lifecycle capability — Stripe/Mercado Pago confirmation and admin alert move to payment approval, while the `BankTransfer` instructions email stays at order creation; (2) the bank-transfer instructions email gains the store WhatsApp number and explicit next-step copy (transfer, then confirm the transfer was sent / send the receipt via WhatsApp). No fiscal/summary content is added.

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
- **THEN** it has the same content structure as before, with no attached document

#### Scenario: Confirmation email from webhook renders in server default culture
- **WHEN** the confirmation email is triggered by a webhook or system path (no user request culture available)
- **THEN** it renders in the server default culture instead of the shopper's culture; emails triggered from a user request keep that request's culture
