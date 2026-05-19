using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Reflection.Metadata;
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
				.HasComputedColumnSql("[Price] / [Quantity]")
				.HasField("_unitPrice");
			modelBuilder.Entity<Fabric>()
				.Property(o => o.PriceMeter)
				.HasComputedColumnSql("[Price] / [Quantity]")
				.HasField("_priceMeter");
			modelBuilder.Entity<GarmentHardware>()
				.Property(o => o.UnitPrice)
				.HasComputedColumnSql("[Price] / [Quantity]")
				.HasField("_unitPrice");

			//Disable the OutputClause that informs a trigger in the column
			//because EF may have possible problems if it doesn’t control potential errors when deleting.

			modelBuilder.Entity<Fabric>()
				.ToTable(tb => tb.UseSqlOutputClause(false));
			modelBuilder.Entity<GarmentHardware>()
				.ToTable(tb => tb.UseSqlOutputClause(false));
			modelBuilder.Entity<Packaging>()
				.ToTable(tb => tb.UseSqlOutputClause(false));
			modelBuilder.Entity<UnitPackagingByCategory>()
				.ToTable(tb => tb.UseSqlOutputClause(false));
			modelBuilder.Entity<UnitFabricByProduct>()
				.ToTable(tb => tb.UseSqlOutputClause(false));
			modelBuilder.Entity<UnitGarmentHardwareByProduct>()
				.ToTable(tb => tb.UseSqlOutputClause(false));


		}
	}
}
