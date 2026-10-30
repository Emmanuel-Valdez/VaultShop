## Why

Customers reach their order history through an offcanvas link labelled "ManageOrder", and the mobile bottom bar spends its fourth slot on "My account" instead of purchases. There is also no in-app signal for order events: customers depend on email, and admins only learn about new orders or transfer confirmations through email.

## What Changes

- Add persisted, per-user order notifications with a header bell (unread count) and a shared inbox page.
- Store only the event type, the order reference, and timestamps; render the localized summary and details at read time from the order. Do not store email HTML or email bodies.
- Customer/Company notifications: payment approved, payment failed, shipped, and cancelled/refunded. No "order placed" notification.
- Admin notifications: new order (company orders at creation, online orders at payment approval) and bank-transfer confirmation request, for every user in the Admin role. Employee receives none.
- Deduplicate notifications per user, order, and type so webhook retries and repeated payment attempts do not duplicate entries.
- Mark all of a user's notifications as read when the inbox is opened; show the 100 most recent.
- Retain notifications for 6 months from `CreatedAt`, including unread ones; delete daily in batches; cascade when a user is deleted.
- Customer mobile bottom bar: authenticated shoppers see Home | Search | Cart | My purchases | Menu. Anonymous shoppers keep "My account" in that slot. "My account" moves to the Menu and the header dropdown, with logout still available wherever it is presented.
- Reuse the existing read-only `/Admin/Order` list and Details pages. No new customer order route.
- Leave transactional email behaviour unchanged.

## Capabilities

### New Capabilities
- `order-notifications`: persisted per-user order notifications, bell counter, shared inbox, event coverage, scoping, deduplication, and retention.

### Modified Capabilities
- `storefront/mobile-app-experience`: the customer bottom bar items and the "My account" entry point change; logout availability must follow the new entry point.
- `order-lifecycle`: customer order history explicitly reuses the shared read-only order list and Details page without exposing order-editing actions.

## Impact

- `VaultShop.Models`: new notification entity.
- `VaultShop.DataAccess`: DbSet, indexes, relationship configuration, repository, unit of work, and one EF Core migration.
- `VaultShop.Web`: notification service and event hooks, bell ViewComponent, shared inbox page, layout navigation, cleanup background service, and localization resources.
- `VaultShop.Tests`: notification service and HTTP tests, plus updates to the existing mobile navigation tests.
- No new external dependencies; email delivery and order state transitions keep their current behaviour.
