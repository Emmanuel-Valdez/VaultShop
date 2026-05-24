# Ukiyo - E-commerce Project

**Live demo:** https://ukiyo.bsite.net

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![Status](https://img.shields.io/badge/status-in%20progress-yellow)
![License](https://img.shields.io/badge/license-source--available-blue)

## Screenshots

### Desktop

![Ukiyo desktop home page](docs/screenshots/home-desktop.png)

### Mobile

![Ukiyo mobile home page](docs/screenshots/home-mobile.png)

## Project Description

Ukiyo is an e-commerce platform specialized in custom anime-inspired backpacks and accessories. The platform features:

### Core Features

- **Customer Store**: Public-facing shop where customers can browse, search, and purchase products with support for both retail and wholesale pricing.

- **Admin Price Calculator**: The administrative panel allows adding products with detailed production costs including:
  - **Fabrics**: Material costs per product
  - **Garment Hardware**: Buttons, zippers, buckles, and other hardware components
  - **Packaging**: Per-category packaging costs
  - **Fixed Costs**: Monthly operational expenses (taxes, rent, utilities, etc.)
  - **Percentage Costs**: Platform fees, payment processing fees, etc.
  
  The system automatically calculates the optimal cost by summing all production expenses and applying profit margins. It then determines:
  - **Fair Wholesale Price**: Based on actual production costs plus a percentage
  - **Suggested Retail Price**: Wholesale price plus an additional margin
  
  **Note**: For marketing and competitive reasons, the final retail price is left to the admin's discretion.

- **Multi-Company Support**: Different companies can manage their own users, and users belonging to the same company can collaboratively manage orders and inventory.

### User Roles

- **Customer**: Regular shoppers who can purchase products
- **Company**: Business accounts with multiple users who can manage orders together
- **Employee**: Company staff members with order management capabilities
- **Admin**: Platform administrators with full system access

## Current Status (May 2026)

### Documentation / Presentation TODO

- [x] Add favicon
- [x] Add screenshots or GIFs of the project, especially mobile views
- [x] Add a step-by-step "How to run locally" section
- [x] Confirm license and update the license badge

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

## How to Run Locally

### Prerequisites

- .NET 8 SDK
- SQL Server or SQL Server Express LocalDB
- Stripe test keys
- Facebook OAuth app credentials, if testing Facebook login

### Steps

1. Clone the repository and enter the project folder.

```powershell
git clone <repository-url>
cd UkiyoMono
```

2. Create `UkiyoMono/.env` with the required variables listed above.

3. Restore dependencies.

```powershell
dotnet restore UkiyoDesigns.sln
```

4. Build the solution.

```powershell
dotnet build UkiyoDesigns.sln
```

5. Run the web app.

```powershell
dotnet run --project UkiyoMono/UkiyoDesignsWeb.csproj --launch-profile https
```

6. Open the local site.

```text
https://localhost:7189/es-AR
```

On first run, the application applies pending EF Core migrations, creates the SQL views and triggers, seeds demo data, repairs missing calculator rows, and creates the admin user from `Seed__AdminEmail` and `Seed__AdminPassword`.

### Database Architecture

The database uses **SQL Views** and **Triggers** for the price calculation system:

**Views:**
- `FixedCostMonthlyView`: Monthly fixed costs calculation
- `TotalPercentageCostView`: Sum of all percentage-based costs
- `CostByProductView`: Total cost breakdown per product (fabrics + hardware + packaging + fixed costs)
- `FinalPriceView`: Calculates wholesale and retail prices based on costs and profit margins

**Triggers:**
- Automatically update unit totals when fabrics, hardware, or packaging quantities/prices change
- Update product-level totals (FabricByProduct, GarmentHardwareByProduct, PackagingByCategory)
### Project Structure

```
UkiyoMono/           # ASP.NET Core MVC Web App
├── Areas/
│   ├── Admin/       # CRUD for products, categories, price calculator
│   ├── Customer/    # Home, Cart, Favorites
│   └── Identity/    # Auth (Login, Register, Manage)
├── Resources/       # .resx files for localization (es-AR/en-US)
├── Program.cs      # Entry point with DotNetEnv configuration
└── .env            # Local secrets (DO NOT COMMIT)

Ukiyo.DataAccess/   # DbContext, Repositories, Migrations
Ukiyo.Models/       # Entities and ViewModels
Ukiyo.Utility/      # Helpers (Stripe, Email, SD)
```

### Tech Stack

- ASP.NET Core 8.0 MVC
- Entity Framework Core (SQL Server)
- Identity + Facebook OAuth
- Stripe Payment Gateway
- Localization (Spanish/English)
- DotNetEnv for secrets management

### Database Entities

**Core:** Product, Category, Company, ApplicationUser, ShoppingCart, OrderHeader, OrderDetail, ProductImage

**Calculator:**
- Fabric, FabricByProduct, UnitFabricByProduct
- GarmentHardware, GarmentHardwareByProduct, UnitGarmentHardwareByProduct
- Packaging, PackagingByCategory, UnitPackagingByCategory
- FixedCost, PercentageCost, PercentageProfit

**SQL Views:** FixedCostMonthly, TotalPercentageCost, CostByProduct, FinalPrice
