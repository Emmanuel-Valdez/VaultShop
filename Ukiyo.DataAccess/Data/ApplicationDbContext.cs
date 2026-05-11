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

			modelBuilder.Entity<Category>().HasData(
				new Category { Id = 1, Name = "Backpack", MaxExpectation = 40, AvgShippingCost = 10000 },
				new Category { Id = 2, Name = "PhoneHolder", MaxExpectation = 150, AvgShippingCost = 3500 },
				new Category { Id = 3, Name = "Jacket", MaxExpectation = 80, AvgShippingCost = 5000 }
				);
			modelBuilder.Entity<Company>().HasData(
				new Company { Id = 1, Name = "Hola Mundo", StreetAddress = "España 2022", State = "Illinois", City = "Florida", PostalCode = "2022", PhoneNumber = "251645468" },
				new Company { Id = 2, Name = "Moure dev", StreetAddress = "madrid 200", State = "nos vimos", City = "jupiter", PostalCode = "200", PhoneNumber = "3333" },
				new Company { Id = 3, Name = "MiduDev", StreetAddress = "francias segundo", State = "madagastar", City = "marte", PostalCode = "101", PhoneNumber = "222222" }
				);
			modelBuilder.Entity<Product>().HasData(
				new Product
				{
					Id = 1,
					Name = "Reconnaissance legion",
					Description = "BackPack inspired in the reconnaissance legion of Attack On Titan",
					FinalRetailPrice = 34900,
					FinalWholesalePrice = 18390,
					CategoryId = 1

				},
				new Product
				{
					Id = 2,
					Name = "Akkatsuki Litle Bag",
					Description = "Little Bag inspired in the criminal organization of shinoby in Naruto series",
					FinalRetailPrice = 44900,
					FinalWholesalePrice = 26567,
					CategoryId = 2
				},
				new Product
				{
					Id = 3,
					Name = "Haikyu Hoodie ",
					Description = "Hooded sweatshirt inspired in the indumentary used by the Karasuno Team on Haikyu animated series",
					FinalRetailPrice = 43234,
					FinalWholesalePrice = 30060.78m,
					CategoryId = 3
				}
				);
			modelBuilder.Entity<GarmentHardwareByProduct>().HasData(
				new GarmentHardwareByProduct { Id = 1, ProductId = 1 },
				new GarmentHardwareByProduct { Id = 2, ProductId = 2 },
				new GarmentHardwareByProduct { Id = 3, ProductId = 3 }
				);
			modelBuilder.Entity<FabricByProduct>().HasData(
				new FabricByProduct { Id = 1, ProductId = 1 },
				new FabricByProduct { Id = 2, ProductId = 2 },
				new FabricByProduct { Id = 3, ProductId = 3 }
				);
			modelBuilder.Entity<PackagingByCategory>().HasData(
				new PackagingByCategory { Id = 1, CategoryId = 1 },
				new PackagingByCategory { Id = 2, CategoryId = 2 },
				new PackagingByCategory { Id = 3, CategoryId = 3 }
				);

			modelBuilder.Entity<PercentageProfit>().HasData(
				new PercentageProfit
				{
					Id = 1,
					Retail = 5,
					Wholesale = 5
				});

			modelBuilder.Entity<FixedCost>().HasData(
				new FixedCost
				{
					Id = 1,
					Name = "Municipal Taxes",
					Cost = 20000
				});
			modelBuilder.Entity<PercentageCost>().HasData(
				new PercentageCost
				{
					Id = 1,
					Name = "Store",
					Percentage = 2
				});
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
