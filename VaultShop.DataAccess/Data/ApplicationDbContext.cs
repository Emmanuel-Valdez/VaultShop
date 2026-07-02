using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VaultShop.Models;
using VaultShop.Models.CalculatorModels;
using VaultShop.Models.ViewModels;

namespace VaultShop.DataAccess.Data
{
	public class ApplicationDbContext : IdentityDbContext<IdentityUser>
	{

		public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
		{

		}

		public DbSet<Category> Categories { get; set; }
		public DbSet<Product> Products { get; set; }
		public DbSet<ApplicationUser> ApplicationUsers { get; set; }
		public DbSet<Company> Companies { get; set; }
		public DbSet<ProductImage> ProductImages { get; set; }
		public DbSet<ShoppingCart> ShoppingCarts { get; set; }
		public DbSet<OrderDetail> OrderDetails { get; set; }
		public DbSet<OrderHeader> OrderHeaders { get; set; }
		public DbSet<Packaging> Packagings { get; set; }
		public DbSet<PackagingByCategory> PackagingsByCategory { get; set; }
		public DbSet<UnitPackagingByCategory> UnitsPackagingByCategory { get; set; }
		public DbSet<Fabric> Fabrics { get; set; }
		public DbSet<FabricByProduct> FabricsByProduct { get; set; }
		public DbSet<UnitFabricByProduct> UnitsFabricByProduct { get; set; }
		public DbSet<GarmentHardware> GarmentHardwares { get; set; }
		public DbSet<GarmentHardwareByProduct> GarmentHardwaresByProduct { get; set; }
		public DbSet<UnitGarmentHardwareByProduct> UnitsGarmentHardwareByProduct { get; set; }
		public DbSet<FixedCost> FixedCosts { get; set; }
		public DbSet<PercentageCost> PercentageCosts { get; set; }
		public DbSet<PercentageCostWholesale> PercentageCostsWholesale { get; set; }
		public DbSet<PercentageProfit> PercentageProfits { get; set; }
		public DbSet<FavoriteProduct> FavoriteProducts { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{

			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<PercentageCostWholesale>()
				.ToTable("PercentageCostsWholesale");

			//Columns calculated automatically when update prices
			modelBuilder.Entity<Packaging>()
				.Property(o => o.UnitPrice)
				.HasComputedColumnSql("\"Price\" / NULLIF(\"Quantity\", 0)", stored: true)
				.HasField("_unitPrice");
			modelBuilder.Entity<Fabric>()
				.Property(o => o.PriceMeter)
				.HasComputedColumnSql("\"Price\" / NULLIF(\"Quantity\", 0)", stored: true)
				.HasField("_priceMeter");
			modelBuilder.Entity<GarmentHardware>()
				.Property(o => o.UnitPrice)
				.HasComputedColumnSql("\"Price\" / NULLIF(\"Quantity\", 0)", stored: true)
				.HasField("_unitPrice");

			// SQL Server-specific UseSqlOutputClause configuration was removed for the PostgreSQL provider migration.
			// Trigger/view behavior will be redesigned or rewritten in the PostgreSQL migration baseline step.

		}
	}
}
