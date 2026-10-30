## 1. Data model and persistence

- [x] 1.1 Add `UserNotification` and `OrderNotificationType` to `VaultShop.Models`; verify `dotnet build VaultShop.sln` succeeds with the new types
- [x] 1.2 Configure `ApplicationDbContext` with the DbSet, string enum conversion, cascade FKs to `ApplicationUser` and `OrderHeader`, the order navigation, and the unique `(ApplicationUserId, OrderId, Type)`, `(ApplicationUserId, CreatedAt)`, and `(CreatedAt)` indexes; verify a build and the generated EF model show the unique index
- [x] 1.3 Add `IUserNotificationRepository`/`UserNotificationRepository` and the `IUnitOfWork.UserNotification` property following the existing repository pattern; verify the solution builds and the existing repository tests still pass
- [x] 1.4 Create the `AddUserNotifications` migration with `dotnet ef migrations add AddUserNotifications --project VaultShop.DataAccess --startup-project VaultShop.Web`; verify the migration creates the table, indexes, and cascade foreign keys

## 2. Notification service

- [x] 2.1 Implement `IOrderNotificationService` with `NotifyOrderEventAsync(orderId, type)`, loading the order once and resolving the customer from `ApplicationUserId`
- [x] 2.2 Implement the Admin fan-out with `UserManager.GetUsersInRoleAsync(SD.Role_Admin)`, inserting one row per administrator and excluding Employee
- [x] 2.3 Implement existence checks and unique-violation handling so repeated calls for the same `(user, order, type)` are a no-op; catch and log persistence failures without failing the caller
- [x] 2.4 Register the service in `Program.cs`; verify the app starts and the service resolves in a test host
- [x] 2.5 Add unit tests for customer creation, admin fan-out, Employee exclusion, guest orders, duplicate delivery, and failure logging

## 3. Event hooks

- [x] 3.1 Emit `PaymentApproved` from `PaymentStatusService.MarkCheckoutSessionPaid` and `ApproveManualBankTransfer` after the real state transition; verify the hook tests pass
- [x] 3.2 Emit `PaymentFailed` from `PaymentStatusService.MarkCheckoutSessionFailed`; verify one notification survives repeated failed attempts for the same order
- [x] 3.3 Emit `Shipped` from `OrderController.ShipOrder` after the order is saved
- [x] 3.4 Emit exactly one of `Cancelled` or `Refunded` from `OrderController.CancelOrder`, choosing `Refunded` when the refund succeeded
- [x] 3.5 Emit `AdminNewOrder` for company orders at creation in `CartController` and for non-company online orders from the paid transitions in `PaymentStatusService`
- [x] 3.6 Emit `AdminBankTransferRequest` from `OrderController.ConfirmTransferSent`, moving both the admin email (`TrySendAdminBankTransferConfirmationRequestAsync`) and the notification inside the `if (!orderHeader.TransferConfirmedByCustomerAt.HasValue)` guard so they fire only on the first confirmation; keep the timestamp update and `Save()` in the same guard
- [x] 3.7 Add integration tests that exercise each hook and confirm an email failure or missing admin email does not suppress the notification

## 4. Notification bell

- [x] 4.1 Add `NotificationBellViewComponent` that returns the signed-in user's unread count and an empty result for anonymous or Employee users
- [x] 4.2 Add the bell view with an accessible label, an optional unread badge, and a link to the inbox; verify the badge and label render correctly
- [x] 4.3 Render the bell in the shared header next to the cart for authenticated customers, company users, and administrators; verify anonymous and Employee requests do not render it
- [x] 4.4 Add HTTP tests for the unread count, zero-state bell, and hidden states

## 5. Notification inbox

- [x] 5.1 Add the authorized Razor Page at `/Identity/Account/Notifications` that loads only the current user's 100 most recent notifications with their orders and marks all unread rows as read in one update
- [x] 5.2 Render each notification as a link to the existing order Details page, with localized title, order reference, event-specific details, date, and an accessible unread indicator
- [x] 5.3 Add es-AR and en-US resources for the bell, inbox, every notification type, the empty state, and the date/details labels; verify `ResxParityTests` passes
- [x] 5.4 Add HTTP tests for cross-user isolation, newest-first ordering, mark-all-read, language switching, empty state, and links to Details

## 6. Customer navigation

- [x] 6.1 Change the authenticated customer bottom bar to Home | Search | Cart | My purchases | Menu, linking My purchases to the existing `/Admin/Order` list with the existing active state
- [x] 6.2 Keep Home | Search | Cart | My account | Menu for anonymous shoppers, with My account leading to sign in
- [x] 6.3 Rename the offcanvas "ManageOrder" link to My purchases and add a My account link for authenticated shoppers; verify logout remains present in the offcanvas and header dropdown
- [x] 6.4 Update `StorefrontMenuHttpTests` for the new items, the anonymous variant, and My purchases linking to the order list; verify the mobile navigation tests pass

## 7. Retention cleanup

- [x] 7.1 Implement `NotificationCleanupBackgroundService` with a daily `PeriodicTimer`, a 6-month `CreatedAt` cutoff, 500-row ID batches, `ExecuteDeleteAsync`, and count-only logging
- [x] 7.2 Register the hosted service in `Program.cs` and expose the single-run method for tests
- [x] 7.3 Add tests for expired deletion, unread deletion, recent retention, batch limits, user cascade, and untouched order data

## 8. Verification and documentation

- [x] 8.1 Run `dotnet build VaultShop.sln` and confirm success
- [x] 8.2 Run the notification, email-service, order, and storefront menu test groups and confirm they pass
- [x] 8.3 Run the full `dotnet test VaultShop.sln` suite and confirm no regressions
- [x] 8.4 Smoke test with a customer and an administrator: bell count, inbox mark-read, notification link to Details, My purchases, My account in the menu, and logout
- [x] 8.5 Update `.local/context/NEXT-2026-10-29.md` with the final model decisions (event type plus order reference, admin fan-out, AdminBankTransferRequest, no OrderPlaced) and mark the change implemented
