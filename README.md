# Ukiyo - ASP.NET Core E-commerce Case Study

**Live demo:** https://ukiyo.bsite.net

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![Status](https://img.shields.io/badge/status-in%20progress-yellow)
![License](https://img.shields.io/badge/license-source--available-blue)

Ukiyo is a portfolio e-commerce project for custom anime-inspired backpacks and accessories. It started as a traditional ASP.NET Core MVC store and is being evolved into a production-style case study with PostgreSQL, Docker Compose, MinIO/S3-compatible image storage, Stripe payments, automated tests, and deployment-oriented hardening.

## Screenshots

### Desktop

![Ukiyo desktop home page](docs/screenshots/home-desktop.png)

### Mobile

![Ukiyo mobile home page](docs/screenshots/home-mobile.png)

### Product Detail

![Ukiyo product detail desktop page](docs/screenshots/product-detail-desktop.png)

![Ukiyo product detail mobile page](docs/screenshots/product-detail-mobile.png)

### Admin Final Prices

![Ukiyo admin final prices dashboard](docs/screenshots/final-prices-dashboard.png)

## Features

- Customer storefront with product browsing, favorites, cart, checkout, and retail/wholesale pricing.
- Admin product, category, inventory, company, order, and price-management flows.
- Admin pricing calculator for fabrics, garment hardware, packaging, fixed costs, percentage costs, profit margins, and final prices.
- Final price dashboard that compares current storefront prices with calculated cost-based suggestions.
- ASP.NET Core Identity with roles for Customer, Company, Employee, and Admin users.
- Stripe Checkout integration with webhook-based payment status updates.
- Product image upload validation, resizing, metadata persistence, and storage abstraction.
- Localization for Spanish and English.

## Tech Stack

- ASP.NET Core 8 MVC
- Entity Framework Core with PostgreSQL/Npgsql
- ASP.NET Core Identity + Facebook OAuth
- Stripe Checkout
- Resend email provider with fake/local email mode
- Docker and Docker Compose
- MinIO/S3-compatible product image storage
- xUnit, Moq, and SQLite in-memory tests for selected service/integration coverage

## Architecture Highlights

- PostgreSQL is the active database provider. Earlier SQL Server migration history is archived and no longer part of the active runtime path.
- The admin pricing calculator uses `PricingCalculatorService` and EF Core queries instead of SQL Server views/triggers.
- Product image persistence is behind `IImageStorageService`; the app supports local filesystem storage and MinIO.
- `ProductImage.ObjectKey` is the storage identity for uploaded images. `ImageUrl` is used only as the browser display URL.
- Checkout order creation is handled by `CheckoutService` and wrapped in a transaction to avoid partial orders.
- Stripe session creation is behind `IPaymentSessionService`; payment status changes are handled through signed webhooks instead of trusting only browser redirects.
- Production-like environments can disable startup migrations with `Database__RunMigrationsOnStartup=false`.

## Project Structure

```text
UkiyoMono/              ASP.NET Core MVC web app
Ukiyo.DataAccess/      EF Core DbContext, repositories, migrations
Ukiyo.Models/          Domain models and view models
Ukiyo.Utility/         Shared constants and infrastructure helpers
UkiyoDesignsWeb.Tests/ Automated tests
docs/                  Screenshots and archived historical migration files
```

## Configuration

Configuration is supplied through environment variables or ignored `.env` files. Do not commit real secrets.

Common variables:

```text
ConnectionStrings__DefaultConnection
Database__RunMigrationsOnStartup
Stripe__SecretKey
Stripe__PublishableKey
Stripe__WebhookSecret
Facebook__AppId
Facebook__AppSecret
Email__Provider
Email__UseFakeEmailSender
Resend__ApiKey
Resend__FromEmail
Seed__AdminEmail
Seed__AdminPassword
SiteUrl
ImageStorage__Provider
ImageStorage__Minio__Endpoint
ImageStorage__Minio__UseSsl
ImageStorage__Minio__BucketName
ImageStorage__Minio__AccessKey
ImageStorage__Minio__SecretKey
ImageStorage__Minio__PublicBaseUrl
```

For local/demo email behavior, use `Email__Provider=Fake`. For real transactional email, use `Email__Provider=Resend` with a private `Resend__ApiKey` and verified sender.

## Run Locally

Prerequisites:

- .NET 8 SDK
- PostgreSQL or Docker Compose
- Stripe test keys if testing checkout payments
- Facebook OAuth credentials if testing Facebook login

1. Restore dependencies.

```powershell
dotnet restore UkiyoDesigns.sln
```

2. Create `UkiyoMono/.env` with local configuration values.

3. Build the solution.

```powershell
dotnet build UkiyoDesigns.sln
```

4. Run the web app.

```powershell
dotnet run --project UkiyoMono/UkiyoDesignsWeb.csproj --launch-profile https
```

5. Open the local site.

```text
https://localhost:7189/es-AR
```

When `Database__RunMigrationsOnStartup=true`, the app applies pending migrations, ensures roles, and creates the admin user from `Seed__AdminEmail` and `Seed__AdminPassword`. Production configuration defaults this to `false` so schema changes are intentional.

## Docker Compose

The Compose stack runs the web app, PostgreSQL 16, MinIO object storage, and a short-lived MinIO initialization container.

1. Copy the sample Compose environment file and replace placeholders.

```powershell
Copy-Item .env.compose.example .env.compose
```

2. Build and start the stack.

```powershell
docker compose --env-file .env.compose up --build
```

3. Open the app.

```text
http://localhost:8080/es-AR
```

Compose persists PostgreSQL data in `postgres-data` and MinIO objects in `minio-data`. For a fresh local database, temporarily enable startup initialization with `DATABASE_RUN_MIGRATIONS_ON_STARTUP=true`, then set it back to `false` if you want production-like behavior.

Stop containers without deleting data:

```powershell
docker compose --env-file .env.compose down
```

Delete local PostgreSQL and MinIO volumes only when intentionally resetting the local stack:

```powershell
docker compose --env-file .env.compose down -v
```

For Compose image storage, use MinIO settings like:

```env
ImageStorage__Provider=Minio
ImageStorage__Minio__Endpoint=minio:9000
ImageStorage__Minio__UseSsl=false
ImageStorage__Minio__BucketName=product-images
ImageStorage__Minio__PublicBaseUrl=http://localhost:9000/product-images
```

`ImageStorage__Minio__Endpoint` is used by the web container over the Docker network. `ImageStorage__Minio__PublicBaseUrl` is the browser-facing URL for product image display.

Do not expose PostgreSQL or the MinIO console directly on a public server.

## Tests

Run the automated tests:

```powershell
dotnet test UkiyoDesigns.sln
```

Current tests focus on high-value service behavior: upload validation, checkout rules, transactional order creation, Stripe session creation, and payment status updates.

## Deployment Direction

The intended next deployment target is a fresh Ubuntu LTS Oracle/VPS server using Docker Compose behind Nginx or Caddy with HTTPS.

Production-style deployment goals:

- Keep PostgreSQL and MinIO private on the Docker network.
- Expose only HTTP/HTTPS publicly through a reverse proxy.
- Use private environment files or host secrets for configuration.
- Keep `Database__RunMigrationsOnStartup=false` and run migrations intentionally.
- Add PostgreSQL and MinIO backup/restore procedures before treating the server as durable.
- Verify image upload/display/delete and checkout flows after deployment.

## Current Limitations / Next Work

- VPS deployment, HTTPS, backup/restore, and monitoring are planned next.
- Identity 2FA management is intentionally hidden until the QR setup/recovery flow is completed and tested.
- Wholesale pricing should include percentage costs.
- Product Price screens should link directly to the product edit/upload view.

## Portfolio Scope

This is a production-style portfolio project, not an enterprise-scale production system. The focus is demonstrating practical .NET backend skills: database migration, secure file upload handling, object storage, payments, testing, Docker-based deployment, configuration, and operational basics.
