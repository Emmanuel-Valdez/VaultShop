# order-lifecycle Specification

## Purpose
Defines the order lifecycle and email timing rule (Opción B): orders are Pending until payment is confirmed, and user-facing confirmation plus admin alerts are sent only on the transition to paid.

## Requirements

### Requirement: Orders are Pending until payment is confirmed

The system SHALL create all new orders with `OrderStatus=Pending` regardless of role or payment method; `OrderStatus=Approved` SHALL occur only after a successful payment transition (`PaymentStatus=Approved`).

#### Scenario: Customer online/bank-transfer order starts Pending
- **WHEN** a Customer creates an order with Stripe, Mercado Pago, or BankTransfer
- **THEN** the persisted order has `OrderStatus=Pending` and `PaymentStatus=Pending`

#### Scenario: Company order starts Pending with delayed payment
- **WHEN** a Company user creates an order
- **THEN** the persisted order has `OrderStatus=Pending` and `PaymentStatus=DelayedPayment` (not `Approved`)

#### Scenario: Approved only after payment confirmation
- **WHEN** a pending order's payment is confirmed (Stripe/MP webhook or browser sync, or admin bank-transfer approval)
- **THEN** the order transitions to `OrderStatus=Approved` and `PaymentStatus=Approved`

#### Scenario: Company delayed-payment order reaches Approved when paid
- **WHEN** a Company order in `Pending + DelayedPayment` is paid (checkout-session sync/webhook or admin bank-transfer approval)
- **THEN** the order transitions to `Approved + Approved`, not `Pending + Approved`; an order already `InProcess`/`Shipped` keeps that OrderStatus

#### Scenario: Existing Approved Company orders unchanged
- **WHEN** an order created before this change already has `Approved + DelayedPayment`
- **THEN** it remains unchanged; only new orders follow the Pending-until-paid rule

### Requirement: Emails fire per payment-method timing rule

The system SHALL send the user order-confirmation email and the admin new-order alert email on the transition to `PaymentStatus=Approved` for Stripe/Mercado Pago orders, and SHALL NOT send either at creation for those methods. `BankTransfer` orders SHALL receive the user confirmation email (transfer instructions + store WhatsApp) at order creation; the admin alert for `BankTransfer` SHALL fire only on admin approval. Re-delivery of the same paid event SHALL NOT duplicate emails.

#### Scenario: No email at Stripe/Mercado Pago order creation
- **WHEN** a Customer creates an order with Stripe or Mercado Pago
- **THEN** neither the user confirmation nor the admin alert has been sent

#### Scenario: Bank-transfer order receives instructions email at creation
- **WHEN** a Customer creates an order with `BankTransfer`
- **THEN** the user confirmation email is sent immediately with the CBU/alias instructions, the store WhatsApp number, and copy stating the customer must confirm the transfer was sent or send the receipt via WhatsApp; the admin alert has NOT been sent

#### Scenario: Emails sent on Stripe/Mercado Pago paid transition
- **WHEN** `MarkCheckoutSessionPaid` successfully transitions a `Pending + Pending` order to paid
- **THEN** both the user confirmation and the admin alert are sent

#### Scenario: Emails sent on bank-transfer approval
- **WHEN** an admin approves a `BankTransfer` order via `ApproveManualBankTransfer`
- **THEN** the admin alert is sent; the user confirmation is not duplicated (already sent at creation, guard no-ops)

#### Scenario: Idempotency on re-delivery
- **WHEN** the same paid webhook or browser sync is delivered again for an already-paid order
- **THEN** the transition is treated as duplicate and no additional confirmation or admin alert is sent

#### Scenario: Concurrent webhook and browser sync send a single email
- **WHEN** the Stripe/Mercado Pago webhook and the customer's browser sync arrive concurrently for the same unpaid order
- **THEN** exactly one confirmation email and one admin alert are sent; the loser's timestamp claim is rejected and it skips sending

### Requirement: OrderDetail variant snapshot is immutable

Every `OrderDetail` row created from a cart line with a variant SHALL persist the variant reference plus a frozen human-readable `VariantLabel` text snapshot (e.g. `Casa: Gryffindor, Tamaño: 15"`), captured at order creation like `Price`. Renaming, disabling, or deleting the variant afterwards SHALL NOT alter past orders. Frozen-after-shipped semantics extend to the variant label, and the label SHALL be shown wherever order lines are displayed (order details, confirmation, summary HTML/PDF, emails).

#### Scenario: Order captures variant snapshot

- **WHEN** a user checks out a cart line for variant `Gryffindor / 15"`
- **THEN** its `OrderDetail` row stores the variant reference and a `VariantLabel` describing the selected values

#### Scenario: Rename does not rewrite history

- **WHEN** an admin later renames value `Black` to `Negro`
- **THEN** existing `OrderDetail` rows still show the label captured at purchase

#### Scenario: Disabled variant keeps history readable

- **WHEN** an order references a variant that is later disabled or deleted
- **THEN** the order still displays its frozen `VariantLabel`

#### Scenario: Variant-less order lines unchanged

- **WHEN** an order line comes from a product with no variants
- **THEN** no variant label is stored or displayed for that line
