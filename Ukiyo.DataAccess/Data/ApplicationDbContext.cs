using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UkiyoDesigns.Models;
using UkiyoDesigns.Models.CalculatorModels;
using UkiyoDesigns.Models.CalculatorModels.SQLViews;
using UkiyoDesigns.Models.ViewModels;

namespace UkiyoDesigns.DataAccess.Data
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
		public DbSet<FixedCostMonthly> FixedCostMonthlyView { get; set; }
		public DbSet<PercentageCost> PercentageCosts { get; set; }
		public DbSet<TotalPercentageCost> TotalPercentageCostView { get; set; }
		public DbSet<CostByProductView> CostByProductViews { get; set; }
		public DbSet<PercentageProfit> PercentageProfits { get; set; }
		public DbSet<FinalPriceView> FinalPriceView { get; set; }
		public DbSet<FavoriteProduct> FavoriteProducts { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{

			base.OnModelCreating(modelBuilder);

			//Views that calculate automatically 
			modelBuilder.Entity<FixedCostMonthly>()
				.HasNoKey()
				.ToView("FixedCostMonthlyView");
			modelBuilder.Entity<TotalPercentageCost>()
				.HasNoKey()
				.ToView("TotalPercentageCostView");
			modelBuilder.Entity<CostByProductView>()
				.HasNoKey()
				.ToView("CostByProductView");
			modelBuilder.Entity<FinalPriceView>()
				.HasNoKey()
				.ToView("FinalPriceView");

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
