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

## Next Steps From Tomorrow
1. [ ] Add favicon (already listed as pending)
2. [ ] Add screenshots or GIFs of the project to README so the mobile view does not look empty
3. [ ] Add a step-by-step "How to run locally" section to README
4. [ ] Add README badges: .NET version, license, and project status
5. [ ] Highlight the deploy link at the top of README: https://ukiyo.bsite.net
6. [ ] Complete and run the manual testing checklist below

## Manual Testing Checklist
1. [ ] Home page loads correctly on desktop
2. [ ] Home page loads correctly on mobile
3. [ ] Language routing works for `es-AR` and `en-US`
4. [ ] Product listing and product details pages load
5. [ ] Cart add/update/remove flow works
6. [ ] Favorites flow works for authenticated users
7. [ ] Login, register, logout, and account pages work
8. [ ] Admin product/category CRUD works
9. [ ] Price calculator pages load and update calculated values correctly
10. [ ] Checkout starts correctly and Stripe configuration is valid
11. [ ] Order status management works from the admin area
12. [ ] Social links and email configuration use environment variables correctly

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
