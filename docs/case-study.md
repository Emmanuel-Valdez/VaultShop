# VaultShop Case Study

Live at `https://vaultshop.evaldez.ar` (+ `https://ukiyostudio.evaldez.ar` second tenant) on Ubuntu 24.04 VPS: Docker Compose, PostgreSQL 16, MinIO S3, Stripe + Mercado Pago + Bank Transfer, Identity + Google OAuth, Nginx HTTPS. Portfolio goal: take an MVC store beyond `dotnet run` into something buildable, deployable, operable, and explainable.

## Goal
Not a marketplace — a production-style portfolio that proves practical .NET backend + infra: migrations, file/object storage, payments, testing, deploy, ops basics, without pretending to be HA.

## Key Decisions

**PostgreSQL + Npgsql.** Migrated from SQL Server assumptions (migrations, queries, pricing service) — one provider, no dual support.

**Pricing in the app.** Removed SQL views/triggers; `PricingCalculatorService` + EF does retail/wholesale % costs + margins portably and testably.

**Object storage.** `IImageStorageService` → `Local` (dev) / `Minio` (prod); `ObjectKey` is identity, `ImageUrl` is display.

**Transactional checkout.** `CheckoutService` wraps order creation; status driven by signed webhooks + server-side session/preference lookup, never bare redirects; Company delayed-payment decouples order from payment but still transactional.

**Server-side order summary.** Internal "Resumen de pedido" HTML + QuestPDF (community license) from snapshot (`Company` fiscal snapshotted), explicitly non-fiscal; ARCA deferred.

**Pagination that fits.** In-memory `PagedList<T>` 12/page keeps DB simple; `pageNumber` param avoids Razor Pages `page` collision.

**Single codebase, two stores.** Platform compose owns PG/MinIO/network; per-store compose per domain/DB/bucket/DP keys; cross-DB/bucket denied — no multi-tenancy code.

**Security hardening last mile.** Global + Login `RateLimiter` (per-IP fixed-window 429), Identity `Lockout`, `UseStatusCodePagesWithReExecute` branded 404/500 (sakura, localized, keeps status), `ForwardedHeaders` for OAuth, `/health/live` + `/health/ready` (DB+storage).

## Operations Evidence
- Reboot/container `unless-stopped` verified; images survive restart
- Backups automated per-store (`do-backup.sh` `pg_dump -Fc` + `mc mirror` → `.dump`/`.tar.gz`, 48/168h freshness + disk checks, cron weekly/daily)
- Restore tested locally (`pg_restore --no-owner` + `mc mirror` API upload) — no volume-tarring
- Uptime/TLS external monitor; runbook `docs/operations/runbook.md` documents every check; webhook/restart visibility via `docker logs | grep`

## What This Demonstrates
MVC + EF Core/PG, Identity roles, Google OAuth, multi-provider payments + webhooks + refunds + reconciliation, object storage (`IImageStorageService` Local/MinIO), pagination + keyword/collection taxonomy (slug, hero, additive filters), per-product pricing (`PricingCalculatorService` `product.MaxExpectation`), admin wholesale/retail preview (`PricingHelper` unified), stock/inventory guards, internal PDF (QuestPDF), Docker/Nginx, l10n, rate limiting/lockout/health, backup/restore, 293 tests.

## Current Limitations
- Portfolio production-style, not HA; observability is uptime/TLS + log greps + health probes
- Fiscal integration (ARCA/CAE) deferred; order summary ("Resumen de pedido") is intentionally non-fiscal
- Restore drills manual; MP sandbox has no real webhook (simulator coverage)
- Next: category images (`plans/vaultshop-new-specs.md#7`); deferred: slugs, variants, coupons — see `openspec/specs/` (pricing, admin-preview, catalog/keywords/collection-hero shipped `2026-09-14`/`15`/`17`/`18`)

## Interview Summary
Self-hosted ASP.NET Core 8 e-commerce with two isolated stores on shared infra, demonstrating backend + payments + storage + deployment + ops with a lean, test-covered codebase.
