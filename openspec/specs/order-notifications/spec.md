# order-notifications Specification

## Purpose

Gives shoppers, company users, and administrators a persisted in-app record of order events, with an unread bell counter and a shared inbox scoped to the signed-in user.

## Requirements

### Requirement: Unread notification bell

Authenticated customers, company users, and administrators SHALL see a notification bell in the storefront header with the count of their unread order notifications. Anonymous shoppers and users without one of those roles SHALL NOT see the bell.

#### Scenario: Customer with unread notifications
- **WHEN** an authenticated customer has unread order notifications
- **THEN** the header shows a bell with the unread count

#### Scenario: No unread notifications
- **WHEN** an authenticated customer has no unread order notifications
- **THEN** the bell is shown without a count badge

#### Scenario: No bell for anonymous or Employee
- **WHEN** the shopper is anonymous or only has the Employee role
- **THEN** no notification bell is rendered

### Requirement: Customer order-event notifications

Customers and company users SHALL receive a notification when an order they can access is paid, when a payment fails, when it ships, and when it is cancelled or refunded. Creating an order without a completed payment SHALL NOT create a notification.

#### Scenario: Payment approved
- **WHEN** an order's payment is approved
- **THEN** the order's customer receives one payment-approved notification

#### Scenario: Payment failed
- **WHEN** an order's payment attempt fails
- **THEN** the order's customer receives one payment-failed notification, and additional failed attempts for the same order do not create duplicates

#### Scenario: Shipped
- **WHEN** an order is marked as shipped
- **THEN** the order's customer receives one shipped notification

#### Scenario: Cancelled with refund
- **WHEN** an order is cancelled and its payment is refunded
- **THEN** the order's customer receives one refunded notification

#### Scenario: Cancelled without refund
- **WHEN** an order is cancelled without a refund
- **THEN** the order's customer receives one cancelled notification

#### Scenario: Order creation alone
- **WHEN** an order is created but not paid
- **THEN** no customer notification is created for the creation event

### Requirement: Administrator order-event notifications

Administrators SHALL receive an in-app notification for every new order and for every bank-transfer confirmation request. Company orders SHALL notify administrators when the order is created; online orders SHALL notify administrators when payment is approved. Every user in the Admin role SHALL receive their own copy. Users with only the Employee role SHALL NOT receive these notifications.

#### Scenario: Company order created
- **WHEN** a company user creates an order with delayed payment
- **THEN** each administrator receives one new-order notification

#### Scenario: Online order paid
- **WHEN** an online order is paid
- **THEN** each administrator receives one new-order notification

#### Scenario: Bank transfer confirmed by customer
- **WHEN** a customer confirms they sent a bank transfer
- **THEN** each administrator receives one bank-transfer confirmation-request notification

#### Scenario: Employee does not receive admin notifications
- **WHEN** an employee is signed in
- **THEN** they receive no new-order or bank-transfer notifications

### Requirement: Notification content is localized and derived

A notification SHALL identify the order, the event type, and the event date. Its summary and details SHALL be rendered at read time in the current culture from the order's current data. The system SHALL NOT store email bodies or rendered HTML in notifications.

#### Scenario: Language switch
- **WHEN** a shopper switches the interface language and opens the inbox
- **THEN** existing notifications are displayed in the new language

#### Scenario: Shipped notification details
- **WHEN** a shipped notification is displayed and the order has tracking information
- **THEN** the notification shows the order reference, tracking number, and carrier

#### Scenario: Notification links to the order
- **WHEN** a shopper activates a notification
- **THEN** the existing order Details page opens for that order

### Requirement: Notifications are scoped and deduplicated

Every notification SHALL belong to exactly one user and one order. A user SHALL see only their own notifications. The system SHALL create at most one notification per user, order, and event type.

#### Scenario: Cross-user isolation
- **WHEN** two users have notifications for different orders
- **THEN** each user's inbox and bell count include only their own notifications

#### Scenario: Duplicate event delivery
- **WHEN** the same paid, failed, shipped, or admin event is delivered more than once for an order
- **THEN** the recipient still has only one notification for that event type and order

#### Scenario: Guest order
- **WHEN** an order has no authenticated user
- **THEN** no customer notification is created for it

### Requirement: Inbox marks notifications as read

Opening the notification inbox SHALL mark all of the signed-in user's unread notifications as read. The inbox SHALL list the 100 most recent notifications, newest first.

#### Scenario: Opening the inbox
- **WHEN** a user with unread notifications opens the inbox
- **THEN** all their notifications become read and the bell count clears

#### Scenario: Inbox ordering
- **WHEN** a user has more than 100 notifications
- **THEN** the inbox shows the 100 most recent, newest first

### Requirement: Notification delivery is independent of email

An in-app notification SHALL be created for its event even when transactional email is not configured, fails to send, or is retried.

#### Scenario: Email delivery fails
- **WHEN** a transactional email fails for an order event that creates a notification
- **THEN** the notification is still present in the recipient's inbox

#### Scenario: Email is not configured
- **WHEN** the admin email address is not configured and a new order is created
- **THEN** administrators still receive the in-app new-order notification

### Requirement: Notification retention

Notifications SHALL be retained for 6 months from their creation time, including unread notifications. A daily background cleanup SHALL delete older notifications in batches and SHALL NOT delete order data. Deleting a user SHALL delete that user's notifications.

#### Scenario: Expired notifications are deleted
- **WHEN** the cleanup runs and a notification is older than 6 months
- **THEN** the notification is deleted even if it was never read

#### Scenario: Recent notifications are kept
- **WHEN** the cleanup runs and a notification is not older than 6 months
- **THEN** the notification remains available

#### Scenario: User deletion cascades
- **WHEN** a user is deleted
- **THEN** all of that user's notifications are deleted

#### Scenario: Order data is preserved
- **WHEN** the cleanup deletes expired notifications
- **THEN** the related orders and order details are not modified
