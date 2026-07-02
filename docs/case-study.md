# VaultShop Case Study

VaultShop is a portfolio e-commerce application built to practice practical .NET backend development, deployment, and operations. It is live at `https://vaultshop.evaldez.ar` and runs on an Ubuntu VPS with Docker Compose, PostgreSQL, MinIO, Stripe, Identity roles, localization, and Nginx HTTPS.

## Goal

The goal was not to build an enterprise marketplace. The goal was to take an ASP.NET Core MVC application beyond local development and turn it into a production-style project that can be built, deployed, operated, backed up, restored, and explained in interviews.

## Key Decisions

### PostgreSQL Instead Of SQL Server Runtime Assumptions

The project was migrated from SQL Server assumptions to PostgreSQL/Npgsql. This required more than changing a connection string: active migrations, EF provider behavior, pricing queries, and deployment configuration had to align with PostgreSQL.

### Pricing Logic In Application Services

Earlier SQL Server-specific pricing views/triggers were removed from active runtime paths. Pricing calculations now use service/EF paths, making the logic easier to test and portable across PostgreSQL deployments.

### Object Storage For Product Images

Uploaded product images are stored through `IImageStorageService`. The current VPS deployment uses MinIO as an S3-compatible object storage service. This keeps images out of the repository and app container while still allowing a local filesystem provider for simpler environments.

### Transactional Checkout

Checkout order creation is wrapped in a transaction to avoid partial orders. Stripe session creation is abstracted behind a payment session service, and payment status is updated through signed webhooks rather than trusting browser redirects alone.

### Production-Style Deployment

The public deployment uses Docker Compose behind host-level Nginx HTTPS. PostgreSQL and MinIO remain private. Startup migrations are disabled in production-style operation so schema changes are intentional deployment steps.

### Backup And Restore Validation

PostgreSQL and MinIO backups were copied from the VPS to the local PC and restored in clean local containers. This verified that the backups are usable, not just created.

## Current Architecture

See `docs/architecture.md` for the architecture diagram and deployment boundaries.

## Operations Evidence

The project now has a lightweight operations baseline:

- VPS/container restart recovery verified.
- Product image persistence verified after restart.
- PostgreSQL backup and restore tested.
- MinIO backup and restore tested.
- External uptime/TLS monitoring enabled.
- Operational runbook documented in `docs/operations/runbook.md`.

## What This Demonstrates

- ASP.NET Core MVC application development.
- EF Core with PostgreSQL/Npgsql.
- Identity roles and admin/customer flows.
- Stripe Checkout/webhook integration.
- Product image upload and object storage design.
- Docker Compose deployment on a Linux VPS.
- Nginx HTTPS reverse proxy setup.
- Practical backup/restore and monitoring basics.

## Current Limitations

- This is a production-style portfolio project, not a high-availability production system.
- Backups are manual/documented, not fully automated yet.
- Monitoring is lightweight uptime/TLS monitoring, not full observability.
- The storefront UI has been refreshed for the portfolio path; future work should focus on deployment evidence, screenshots, CI, and documentation/deployment hardening before more visual redesign.

## Interview Summary

VaultShop is a self-hosted ASP.NET Core 8 e-commerce app deployed on a real Ubuntu VPS. It demonstrates practical backend and infrastructure work: PostgreSQL migration, object storage, payments, Docker deployment, HTTPS reverse proxy, backup/restore validation, and operational documentation.
