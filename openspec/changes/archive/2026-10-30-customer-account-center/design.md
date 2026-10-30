## Context

See `proposal.md` for motivation and `specs/` for the behavior contract.

The shared order list at `Areas/Admin/Order` is already role-adaptive: `GetAll` filters rows server-side and `OrderAccessPolicy` gates Details. The layout already exposes a customer bottom bar, an offcanvas menu, and a header user dropdown with logout. Order emails are sent through `TransactionalEmailService`, and `PaymentReconciliationBackgroundService` is the existing pattern for a daily hosted service.

Constraints:

- The app is bilingual (es-AR/en-US); users can change language at any time.
- EF Core runs on PostgreSQL in production and SQLite in tests; tests use `EnsureCreated`, not migrations.
- The order list and Details page must stay the only order-history surface; a second customer route would duplicate controller, view, and JavaScript behaviour.
- Transactional email must keep its current behaviour and tests.

## Goals / Non-Goals

**Goals:**

- Persist order-event notifications per user without storing email content.
- Make in-app notifications independent of SMTP success or configuration.
- Cover the meaningful order events without notifying on abandoned or unpaid order creation.
- Reuse the existing order list and navigation shell.
- Keep the change testable at the service and HTTP levels.

**Non-Goals:**

- Push notifications, real-time updates, websockets, or email changes.
- MercadoPago payment-failure transitions; that provider currently does not transition failures.
- Notifications for Employee users.
- A new customer-facing order URL.
- Backfilling notifications for historical orders.
- Per-notification push or digest preferences.

## Decisions

### Store event type and order reference, render content at read time

`UserNotification` stores `ApplicationUserId`, `OrderId`, `Type`, `CreatedAt`, and `ReadAt`. It stores no subject, body, or rendered HTML.

The inbox renders the title and details from the event type plus the order's current data (`Name`, `OrderTotal`, `TrackingNumber`, `Carrier`, payment method), using the current culture's resources.

Alternatives considered:

- **Store the email body** — rejected: tens of KB per row for 6 months, brand-heavy email markup, frozen language, and stored-HTML/XSS surface.
- **Render a short HTML/text at event time** — rejected: still freezes the language and duplicates presentation logic; the event type plus the order reference is sufficient and stays current.
- **Store the current order status** — rejected: the notification must describe the event that happened, not the order's latest state.

### Use a dedicated notification service, not the email service

`IOrderNotificationService.NotifyOrderEventAsync(orderId, type)` owns recipient resolution, deduplication, and persistence. It is called from the domain transition points, not from `TransactionalEmailService`.

Alternatives considered:

- **Write notifications inside `TransactionalEmailService`** — rejected: an in-app notification would depend on SMTP success and would miss events that have no email (cancelled/refunded), while coupling two delivery channels.
- **Write inside controllers only** — rejected: webhook, reconciliation, and manual approval paths would be missed.

Notification failures are caught and logged; they must not roll back an order transition or fail a request.

### Event hooks

| Event | Call point | Recipients |
|---|---|---|
| `PaymentApproved` | `PaymentStatusService.MarkCheckoutSessionPaid` and `ApproveManualBankTransfer` | order customer |
| `PaymentFailed` | `PaymentStatusService.MarkCheckoutSessionFailed` | order customer |
| `Shipped` | `OrderController.ShipOrder` | order customer |
| `Cancelled` or `Refunded` | `OrderController.CancelOrder`; `Refunded` when the refund succeeded, otherwise `Cancelled` | order customer |
| `AdminNewOrder` | `CartController` company-order creation; `PaymentStatusService` when a non-company online order is paid | every Admin user |
| `AdminBankTransferRequest` | `OrderController.ConfirmTransferSent` (only on the first confirmation, inside the `TransferConfirmedByCustomerAt == null` guard) | every Admin user |

`AdminNewOrder` deliberately matches the existing admin-email timing: company orders at creation, online orders only after payment. Abandoned card checkouts do not reach an administrator.

`ConfirmTransferSent` currently sends the admin email on every call (outside the first-confirmation guard). This change moves both the admin email (`TrySendAdminBankTransferConfirmationRequestAsync`) and the `AdminBankTransferRequest` notification inside the `if (!orderHeader.TransferConfirmedByCustomerAt.HasValue)` block, so they fire exactly once per order even on repeated clicks/reloads. The timestamp update and `Save()` stay inside the same guard. Dedup remains as a backstop for races.

### Recipient resolution and deduplication

- Customer notifications use `OrderHeader.ApplicationUserId`; orders without a user produce none.
- Admin notifications resolve `UserManager.GetUsersInRoleAsync(SD.Role_Admin)` and insert one row per administrator. Employee is excluded.
- A unique index on `(ApplicationUserId, OrderId, Type)` enforces one event per user and order. The service checks existence first and treats a unique-violation race as a no-op.
- `Cancelled` and `Refunded` are mutually exclusive; a successful refund emits only `Refunded`.

### Bell and inbox

- `NotificationBellViewComponent` queries the unread count for the signed-in user and renders a bell with an optional badge. It is shown for authenticated customers, company users, and administrators; hidden for anonymous users and Employees. The count query is indexed and not cached.
- The inbox is a Razor Page at `/Identity/Account/Notifications` with `[Authorize]`, outside the Manage folder so it is not tied to the account-settings sidebar.
- The page loads the current user's notifications with the related order, newest first, limited to 100.
- Opening the page marks all of that user's unread notifications as read in a single update.
- Each notification row is a link to `/Admin/Order/Details?orderId={id}`; the existing page enforces access. No new order route is introduced.
- Content is a `switch` over the notification type using localization keys, with details from the order. A shipped notification includes tracking and carrier only when present.

### Retention

`NotificationCleanupBackgroundService` follows the `PaymentReconciliationBackgroundService` pattern (`BackgroundService`, `PeriodicTimer`, scoped services).

- Runs daily; the first run happens after the first interval tick.
- Cutoff is `DateTime.UtcNow.AddMonths(-6)`, applied to `CreatedAt` regardless of `ReadAt`.
- Deletes in batches of 500: project the oldest matching IDs, then `ExecuteDeleteAsync` for those IDs.
- Logs only the deleted count, never notification or order content.
- Does not touch orders or order details.

### Navigation

- Authenticated shoppers: `Home | Search | Cart | My purchases | Menu`, with My purchases linking to the existing `/Admin/Order` list and `isAdminOrders` driving the active state.
- Anonymous shoppers keep `Home | Search | Cart | My account | Menu`; My account leads to sign in.
- "My account" is added to the offcanvas menu for authenticated users next to the renamed "My purchases" link.
- The header user dropdown already contains My account and Logout; no new logout flow is created.
- The admin bottom bar is unchanged.

Alternatives considered:

- **New `/mis-compras` controller and view** — rejected: duplicates the list, Details wiring, DataTables script, and access rules for a cosmetic URL change. Access is enforced by the server, not the route prefix.

### Data model and indexes

`UserNotification`:

| Column | Notes |
|---|---|
| `Id` | int identity |
| `ApplicationUserId` | required FK to `ApplicationUser`, cascade delete |
| `OrderId` | required FK to `OrderHeader`, cascade delete |
| `Type` | enum stored as string |
| `CreatedAt` | UTC |
| `ReadAt` | nullable UTC |

Indexes: unique `(ApplicationUserId, OrderId, Type)`, `(ApplicationUserId, CreatedAt)` for the inbox and bell, and `(CreatedAt)` for cleanup. A navigation property to `OrderHeader` lets the inbox load order details in one query.

## Risks / Trade-offs

- **Event hooks can drift from email and state transitions** → hooks sit next to the existing state transitions and are covered by service tests; the notification service is the single write path.
- **Duplicate delivery from webhooks or repeated failures** → unique index plus insert-if-absent; duplicate writes are swallowed.
- **Bell count on every page request** → covered by the user/created index; no cache until measurements justify it.
- **Notification details show the order's current data, not a snapshot** → the event type carries the historical meaning; totals are stable and tracking is set when shipping. Accepted.
- **MercadoPago failures are not notified** → no failure transition exists today; adding provider failure handling is a separate change.
- **Admin fan-out cost per order event** → one role lookup per admin event; admin counts are small. Accepted.
- **Notification write failure must not break checkout, shipping, or payment** → calls are best-effort and logged.
- **Cleanup using `ExecuteDeleteAsync` with batches** → the batch is driven by an ID projection, which translates on both Npgsql and SQLite.
- **Razor Page authorization** → `[Authorize]` on the page model; the page is not under the existing authorized Manage folder.

## Migration Plan

1. Add the `UserNotifications` table and indexes with an EF Core migration.
2. Deploy the migration; no order data is rewritten and no notifications are backfilled.
3. Register the cleanup hosted service. The first cleanup runs after the first daily tick.
4. Rollback: stop the cleanup service and drop the table and indexes. Orders, payments, and emails are unaffected.

## Open Questions

None. The deferred items (MercadoPago failure transitions, Employee notifications, push delivery) are explicitly out of scope and do not change these decisions.
