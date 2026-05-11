# UkiyoMono - E-commerce Project

## Current Status (May 2026)

### Environment Variables Setup (.env)

The project uses **DotNetEnv** to manage secrets. The `.env` file is in `.gitignore` and is NOT pushed to GitHub.

**Required variables in `UkiyoMono/.env`:**

```env
# Database Connection
ConnectionStrings__DefaultConnection=your_connection_string

# Stripe Payment Gateway
Stripe__SecretKey=your_stripe_secret_key
Stripe__PublishableKey=your_stripe_publishable_key

# Facebook OAuth
Facebook__AppId=your_facebook_app_id
Facebook__AppSecret=your_facebook_app_secret

# Email Configuration
Email__SmtpHost=your_smtp_host
Email__SmtpPort=your_smtp_port
Email__SmtpUser=your_smtp_user
Email__SmtpPassword=your_smtp_password

# Seed Admin User (created on first run)
Seed__AdminEmail=your_admin_email
Seed__AdminPassword=your_admin_password

# Social Media Links
Social__TikTok=your_tiktok_link
Social__WhatsApp=your_whatsapp_link
Social__Instagram=your_instagram_link
Social__Facebook=your_facebook_link
Social__Evalmon=your_evalmon_link
```

### Changes Made (10/05/2026)

1. **Installed DotNetEnv 3.2.0** - to load `.env` variables
2. **Created `.env`** - contains real values (ConnectionString, Stripe, Facebook, Seed admin credentials)
3. **Created `.env.example`** - template to share on GitHub (no real values)
4. **Modified `Program.cs`** - now loads `.env` with `DotNetEnv.Env.Load()` at the start
5. **Modified Facebook OAuth** - AppId and AppSecret read from `builder.Configuration["Facebook:AppId"]`
6. **Cleaned `appsettings.json` and `appsettings.Production.json`** - no sensitive data
7. **Updated `.gitignore`** - includes `.env` and variants
8. **Modified `DbInitializer.cs`** - admin credentials now come from `Seed:AdminEmail` / `Seed:AdminPassword` (fail-fast if missing)
9. **Modified `SD.cs`** - social media links (TikTok, WhatsApp, Instagram, Facebook, Evalmon) now read from environment variables

### Pending Tasks

- [ ] Delete current `.git` and create a new one for clean GitHub publish
- [ ] Complete missing translations in views (Help, FAQs, AboutUs, etc.)
- [ ] Add favicon/visual branding

### Project Structure

```
UkiyoMono/           # ASP.NET Core MVC Web App
├── Areas/
│   ├── Admin/       # CRUD for products, categories, price calculator
│   ├── Customer/    # Home, Cart, Favorites
│   └── Identity/    # Auth (Login, Register, Manage)
├── Resources/       # .resx files for localization (es-AR/en-US)
├── Program.cs       # Entry point with DotNetEnv configuration
└── .env             # Local secrets (DO NOT COMMIT)

Ukiyo.DataAccess/    # DbContext, Repositories, Migrations
Ukiyo.Models/        # Entities and ViewModels
Ukiyo.Utility/       # Helpers (Stripe, Email, SD)
```

### Tech Stack

- ASP.NET Core 8.0 MVC
- Entity Framework Core (SQL Server)
- Identity + Facebook OAuth
- Stripe Payment Gateway
- Localization (Spanish/English)
- DotNetEnv for secrets

### Important Notes

- Database uses **SQL Views** and **Triggers** for the price calculation system
- 14 admin controllers for complete system management
- Seed data with 3 sample products (categories: Backpack, PhoneHolder, Jacket)

### Coding Conventions

- **ALL code must be in English** - class names, variable names, comments, method names, etc.
- This applies to: C#, Razor views, JavaScript, CSS, resource files (.resx), database objects (tables, columns, views, triggers)
- Only human-readable text content (product descriptions, UI labels) can be in Spanish/English based on localization