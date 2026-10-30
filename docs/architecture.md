# VaultShop Architecture

VaultShop is a self-hosted ASP.NET Core 10 MVC monolith on Ubuntu 24.04 VPS: one web app, one PostgreSQL 16, one MinIO S3, Nginx HTTPS proxy. Two stores (VaultShop + UkiyoStudio) share the platform but are fully isolated per DB/bucket/creds.

```mermaid
flowchart TD
    user[Browser es-AR/en-US] -->|HTTPS| nginx[Nginx 80/443]
    nginx -->|127.0.0.1:8080| vault[VaultShop web]
    nginx -->|127.0.0.1:8083| ukiyo[UkiyoStudio web]
    nginx -->|/product-images| minio[(MinIO buckets)]
    vault -->|Npgsql| pgVault[(vaultshop DB)]
    ukiyo -->|Npgsql| pgUkiyo[(ukiyostudio DB)]
    vault -->|S3 API| minio
    ukiyo -->|S3 API| minio
    vault -->|Checkout/Webhooks| stripe[Stripe]
    vault & ukiyo -->|Checkout/Webhooks| mp[Mercado Pago]
    vault & ukiyo -->|OAuth| google[Google]
    vault & ukiyo -->|health| health[/health/live + /health/ready\]
```

## Deployment Boundaries
- Public only: Nginx 80/443; PG + MinIO private (MinIO API `127.0.0.1:9000` via `/product-images` proxy)
- SSH: Tailscale; `COMPOSE_PROJECT_NAME` stable (`vaultshop-platform`/`vaultshop`/`ukiyostudio`)
- Platform compose (`postgres`, `minio`, volumes, network) + per-store compose (`web` only, loopback port, DP keys)
- Per-store isolation: separate DB + role (cross-DB denied), separate bucket + scoped MinIO user (cross-bucket denied), separate backups

## Application Boundaries
- Images via `IImageStorageService`; `ProductImage.ObjectKey` = storage identity, `ImageUrl` = display URL
- Checkout transactional (`CheckoutService.ExecuteInTransaction`); pagination `PagedList<T>` 12/page; billing QuestPDF from snapshot
- Payments: keyed `IPaymentSessionService`/`IPaymentRefundService` by `SD.PaymentMethod*`; signed webhooks + server-side lookup drive status, browser return only verifies; refund on cancel
- Hardening: `ForwardedHeaders` (OAuth behind proxy), `AddRateLimiter` (Global + Login per-IP 429), `Identity Lockout`, `UseStatusCodePagesWithReExecute` (branded 404/500), `AddHealthChecks` (DB + `StorageHealthCheck`) → `/health/live` (200) / `/health/ready` (200/503)
- Config-driven branding/theme (`Branding__*`, `Theme__*` hex → CSS vars), l10n `es-AR`/`en-US`, `Database__RunMigrationsOnStartup=false` in prod
- Shared mobile app shell (role-aware bottom bar, full-screen search, offcanvas menu) rendered from `_Layout`; admin hub + persisted per-user order notifications (`OrderNotificationService` + 6-month cleanup `BackgroundService`)

## Future Client Deployment
Reuse same codebase shape, new per-store env + compose project + domain + DB/bucket/creds + Stripe/MP secrets + backups/cron. For full VM/K8s isolation, skip shared platform.
