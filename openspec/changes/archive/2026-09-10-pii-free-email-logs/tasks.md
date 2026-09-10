## 1. PII-free email logging

- [x] 1.1 Remove `recipient` from success log in `TransactionalEmailService.TrySendEmailAsync:271` (keep `emailType + orderId`) and verify no test asserts `to {Recipient}`
- [x] 1.2 Remove `recipient` from error log in `TransactionalEmailService.TrySendEmailAsync:275` (keep `emailType + orderId + exception`) and verify error path still logs
- [x] 1.3 Verify no other `ILogger` call in `TransactionalEmailService.cs` interpolates `recipient`, `AdminEmail`, or email body — grep for `Recipient|AdminEmail|Body` in logs is clean

## 2. Verification

- [x] 2.1 Run `dotnet test VaultShop.sln` green and confirm log output for email sends contains `orderId` but not plaintext email addresses
