# observability/email-logging Specification

## Purpose
Ensure transactional email logs never expose personally identifiable information while retaining debuggability via order and email-type identifiers.

## Requirements

### Requirement: Email send logs are PII-free
The system SHALL NOT write recipient email addresses, AdminEmail, or email body content to application logs when sending transactional emails. Logs SHALL contain only PII-free identifiers (orderId, emailType, outcome).

#### Scenario: Successful send does not log recipient address
- **WHEN** a transactional email (order confirmation, payment receipt, failed payment, shipping confirmation, admin alerts) is sent successfully
- **THEN** the log entry contains `orderId` and `emailType` and does not contain the recipient email address or email body

#### Scenario: Failed send does not log recipient address
- **WHEN** a transactional email send fails and the failure is logged
- **THEN** the error log contains `orderId`, `emailType`, and the exception, and does not contain the recipient email address or email body

#### Scenario: Admin alert without PII
- **WHEN** an admin notification email is sent or skipped due to missing AdminEmail configuration
- **THEN** no log entry contains the admin email address; skip logs contain only `orderId` and a reason code

### Requirement: Email log outcome is observable
The system SHALL log a single PII-free entry per transactional email attempt that indicates success or failure, sufficient to correlate with the persisted email-sent timestamp on `OrderHeader` without reading the email content.

#### Scenario: Success is traceable to OrderHeader
- **WHEN** an email is sent and `OrderHeader.*EmailSentUtc` is set
- **THEN** a corresponding PII-free success log exists with the same `orderId` and `emailType`

#### Scenario: Logs remain grep-friendly
- **WHEN** an operator searches logs for a given `orderId`
- **THEN** all related email attempt logs are discoverable by `orderId` alone
