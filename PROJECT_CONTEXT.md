# UkiyoMono - E-commerce Project

## Description
E-commerce for anime-inspired backpacks. Admin panel calculates optimal product prices based on: fabrics, hardware, packaging, fixed costs, and percentage costs. Determines fair wholesale and suggested retail prices (final price set by admin). Supports multi-company users.

## Tech Stack
- ASP.NET Core 8.0 MVC, EF Core (SQL Server), Identity + Facebook OAuth, Stripe, Localization (es/en), DotNetEnv

## Status (11/05/2026)
- ✅ Clean repo with new .git (no sensitive data committed)
- ✅ All secrets in .env (in .gitignore)
- ✅ Facebook, Stripe, DB, admin credentials, social links → environment variables

## Pending Tasks
1. [x] Move SQL Views/Triggers from migration to DbInitializer (after migrations run)
2. [x] Regenerate migrations with demo data (seeds moved to DbInitializer, not in migrations)
3. [x] Complete missing translations (Help, FAQs, AboutUs, etc.) - titles now use @Localizer
4. [ ] Add favicon

## .env Required Variables
```
ConnectionStrings__DefaultConnection
Stripe__SecretKey, Stripe__PublishableKey
Facebook__AppId, Facebook__AppSecret
Email__SmtpHost, Email__SmtpPort, Email__SmtpUser, Email__SmtpPassword
Seed__AdminEmail, Seed__AdminPassword
Social__TikTok, Social__WhatsApp, Social__Instagram, Social__Facebook, Social__Evalmon
```

## Database Views (created in DbInitializer, NOT in migrations)
- FixedCostMonthlyView, TotalPercentageCostView, CostByProductView, FinalPriceView
- Triggers for auto-updating unit totals and product totals

## Coding Conventions
- ALL code in English (classes, variables, comments, DB objects)