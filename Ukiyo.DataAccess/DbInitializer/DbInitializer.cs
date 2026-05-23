using Azure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.Models;
using UkiyoDesigns.Models.CalculatorModels;
using UkiyoDesigns.Utility;


namespace UkiyoDesigns.DataAccess.DbInitializer
{
	public class DbInitializer : IDbInitializer
	{
		private readonly UserManager<ApplicationUser> _userManager;
		private readonly RoleManager<IdentityRole> _roleManager;
		private readonly ApplicationDbContext _db;
		private readonly IConfiguration _configuration;

		public DbInitializer(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
			ApplicationDbContext db, IConfiguration configuration)
		{
			_roleManager = roleManager;
			_userManager = userManager;
			_db = db;
			_configuration = configuration;
		}

		public void Initialize()
		{
			//migrations if there are no applied
			try
			{
				if (_db.Database.GetPendingMigrations().Count() > 0)
				{
					_db.Database.Migrate();
				}
				CreateViewsAndTriggers();
				SeedCalculatorDefaults();
				SeedDemoData();
				EnsureCalculatorRows();
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}



			
			// Create roles if they are not created 
			if (!_roleManager.RoleExistsAsync(SD.Role_Customer).GetAwaiter().GetResult())
			{
				_roleManager.CreateAsync(new IdentityRole(SD.Role_Customer)).GetAwaiter().GetResult();
				_roleManager.CreateAsync(new IdentityRole(SD.Role_Employee)).GetAwaiter().GetResult();
				_roleManager.CreateAsync(new IdentityRole(SD.Role_Admin)).GetAwaiter().GetResult();
				_roleManager.CreateAsync(new IdentityRole(SD.Role_Company)).GetAwaiter().GetResult();

				// Obtain admin credentials from configuration (appsettings or environment variables)
				// In .env they appear as: Seed__AdminEmail and Seed__AdminPassword -> IConfiguration maps to "Seed:AdminEmail"
				var adminEmail = _configuration["Seed:AdminEmail"]
					?? Environment.GetEnvironmentVariable("Seed__AdminEmail");
				var adminPassword = _configuration["Seed:AdminPassword"]
					?? Environment.GetEnvironmentVariable("Seed__AdminPassword");

				// Fail fast: do not use silent default values in production
				if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
				{
					throw new InvalidOperationException("Missing required credentials for admin seed. " +
						"Please provide Seed:AdminEmail and Seed:AdminPassword (or the environment variables Seed__AdminEmail / Seed__AdminPassword).");
				}

				// Create admin user with the provided credentials
				var adminUser = new ApplicationUser
				{
					UserName = adminEmail,
					Email = adminEmail,
					Name = "Emmanuel Valdez",
					PhoneNumber = "1112223333",
					StreetAddress = "StreetDemo",
					State = "Mendoza",
					City = "L.H."
				};

				var createResult = _userManager.CreateAsync(adminUser, adminPassword).GetAwaiter().GetResult();
				if (!createResult.Succeeded)
				{
					var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
					throw new InvalidOperationException("Failed to create admin user: " + errors);
				}

				ApplicationUser? user = _db.ApplicationUsers.FirstOrDefault(u => u.Email == adminEmail);
				if (user == null)
				{
					throw new InvalidOperationException("Failed to load the seeded admin user.");
				}

				_userManager.AddToRoleAsync(user, SD.Role_Admin).GetAwaiter().GetResult();
			}
		}
		private void CreateViewsAndTriggers()
		{
			ExecuteIfNotExists(SqlScripts.View_UpdateFixedCost);
			ExecuteIfNotExists(SqlScripts.View_UpdatePercentageCost);
			ExecuteIfNotExists(SqlScripts.View_CostByProduct);
			ExecuteIfNotExists(SqlScripts.View_FinalPrice);

			ExecuteIfNotExists(SqlScripts.TR_UpdateUnitsTotalFabric);
			ExecuteIfNotExists(SqlScripts.TR_UpdateUnitsTotalGarmentHardware);
			ExecuteIfNotExists(SqlScripts.TR_UpdateUnitsTotalPackaging);

			ExecuteIfNotExists(SqlScripts.TR_UpdateUnitPackaging_UnitsTable);
			ExecuteIfNotExists(SqlScripts.TR_UpdateUnitsTotalFabric_UnitsTable);
			ExecuteIfNotExists(SqlScripts.TR_UpdateUnitsTotalGarmentHardware_UnitsTable);

			ExecuteIfNotExists(SqlScripts.TR_UpdateTotalPackagingByCategory);
			ExecuteIfNotExists(SqlScripts.TR_UpdateTotalFabricByProduct);
			ExecuteIfNotExists(SqlScripts.TR_UpdateTotalGarmentHardwareByProduct);
		}

		private void SeedDemoData()
		{
			var categories = new[]
			{
				new Category { Name = "Backpack", MaxExpectation = 40, AvgShippingCost = 10000 },
				new Category { Name = "Phone Holder", MaxExpectation = 150, AvgShippingCost = 3500 },
				new Category { Name = "Jacket", MaxExpectation = 80, AvgShippingCost = 5000 },
				new Category { Name = "Hoodie", MaxExpectation = 90, AvgShippingCost = 4500 },
				new Category { Name = "Tote Bag", MaxExpectation = 120, AvgShippingCost = 3800 }
			};

			foreach (var category in categories)
			{
				if (!_db.Categories.Any(existingCategory => existingCategory.Name == category.Name))
				{
					_db.Categories.Add(category);
				}
			}

			_db.SaveChanges();

			if (!_db.Companies.Any())
			{
				_db.Companies.AddRange(
					new Company {  Name = "Demo Company 1", StreetAddress = "Demo Street 1", State = "Demo State", City = "Demo City", PostalCode = "12345", PhoneNumber = "1234567890" },
					new Company { Name = "Demo Company 2", StreetAddress = "Demo Street 2", State = "Demo State", City = "Demo City", PostalCode = "12345", PhoneNumber = "0987654321" },
					new Company { Name = "Demo Company 3", StreetAddress = "Demo Street 3", State = "Demo State", City = "Demo City", PostalCode = "12345", PhoneNumber = "1122334455" }
				);
				_db.SaveChanges();
			}

			var categoryIds = _db.Categories
				.Where(category => categories.Select(seedCategory => seedCategory.Name).Contains(category.Name))
				.ToDictionary(category => category.Name, category => category.Id);

			var products = new[]
			{
				new Product { Name = "Reconnaissance Legion Backpack", Description = "Backpack inspired by the reconnaissance legion from Attack on Titan.", FinalRetailPrice = 34900, FinalWholesalePrice = 18390, CategoryId = categoryIds["Backpack"], IsAvailableInStore = true },
				new Product { Name = "Akatsuki Mini Backpack", Description = "Compact backpack inspired by the Akatsuki organization from Naruto.", FinalRetailPrice = 44900, FinalWholesalePrice = 26567, CategoryId = categoryIds["Backpack"], IsAvailableInStore = true },
				new Product { Name = "Totoro Forest Backpack", Description = "Soft everyday backpack inspired by forest spirits.", FinalRetailPrice = 38900, FinalWholesalePrice = 21400, CategoryId = categoryIds["Backpack"], IsAvailableInStore = true },
				new Product { Name = "Kitsune Phone Holder", Description = "Crossbody phone holder with kitsune-inspired details.", FinalRetailPrice = 21900, FinalWholesalePrice = 12800, CategoryId = categoryIds["Phone Holder"], IsAvailableInStore = true },
				new Product { Name = "Sakura Phone Holder", Description = "Light phone holder with cherry blossom accents.", FinalRetailPrice = 19900, FinalWholesalePrice = 11600, CategoryId = categoryIds["Phone Holder"], IsAvailableInStore = true },
				new Product { Name = "Karasuno Flight Hoodie", Description = "Hoodie inspired by Karasuno team colors from Haikyu.", FinalRetailPrice = 43234, FinalWholesalePrice = 30060.78m, CategoryId = categoryIds["Hoodie"], IsAvailableInStore = true },
				new Product { Name = "Uchiha Crest Hoodie", Description = "Warm hoodie with a bold clan crest design.", FinalRetailPrice = 47900, FinalWholesalePrice = 28900, CategoryId = categoryIds["Hoodie"], IsAvailableInStore = true },
				new Product { Name = "Survey Corps Jacket", Description = "Jacket inspired by expedition uniforms.", FinalRetailPrice = 68900, FinalWholesalePrice = 41200, CategoryId = categoryIds["Jacket"], IsAvailableInStore = true },
				new Product { Name = "Akira Capsule Jacket", Description = "Statement jacket inspired by cyberpunk motorcycle culture.", FinalRetailPrice = 74500, FinalWholesalePrice = 45800, CategoryId = categoryIds["Jacket"], IsAvailableInStore = true },
				new Product { Name = "Spirited Bathhouse Tote", Description = "Roomy tote bag inspired by bathhouse iconography.", FinalRetailPrice = 24900, FinalWholesalePrice = 14800, CategoryId = categoryIds["Tote Bag"], IsAvailableInStore = true },
				new Product { Name = "Moon Prism Tote", Description = "Daily tote with magical girl-inspired details.", FinalRetailPrice = 23900, FinalWholesalePrice = 13900, CategoryId = categoryIds["Tote Bag"], IsAvailableInStore = true },
				new Product { Name = "Demon Slayer Utility Tote", Description = "Utility tote with contrasting patterned panels.", FinalRetailPrice = 26900, FinalWholesalePrice = 15900, CategoryId = categoryIds["Tote Bag"], IsAvailableInStore = true }
			};

			foreach (var product in products)
			{
				if (!_db.Products.Any(existingProduct => existingProduct.Name == product.Name))
				{
					_db.Products.Add(product);
				}
			}

			_db.SaveChanges();

			SeedCalculatorInventory();
			EnsureCalculatorRows();
			SeedCalculatorAssignments();
		}

		private void SeedCalculatorDefaults()
		{
			if (!_db.PercentageProfits.Any())
			{
				_db.PercentageProfits.Add(new PercentageProfit
				{
					Retail = 5,
					Wholesale = 5
				});
			}

			var fixedCosts = new[]
			{
				new FixedCost
				{
					Name = "Municipal Taxes",
					Cost = 20000,
					Description = "Monthly municipal taxes."
				},
				new FixedCost { Name = "Workshop Rent", Cost = 180000, Description = "Monthly workshop rent." },
				new FixedCost { Name = "Electricity", Cost = 45000, Description = "Average monthly electricity bill." },
				new FixedCost { Name = "Internet", Cost = 25000, Description = "Monthly internet service." },
				new FixedCost { Name = "Maintenance", Cost = 30000, Description = "Machine and tool maintenance reserve." }
			};

			foreach (var fixedCost in fixedCosts)
			{
				if (!_db.FixedCosts.Any(existingFixedCost => existingFixedCost.Name == fixedCost.Name))
				{
					_db.FixedCosts.Add(fixedCost);
				}
			}

			var percentageCosts = new[]
			{
				new PercentageCost { Name = "Store", Percentage = 2, Description = "Store operating percentage." },
				new PercentageCost { Name = "Payment Processor", Percentage = 6, Description = "Card and payment gateway fees." },
				new PercentageCost { Name = "Marketplace", Percentage = 12, Description = "Marketplace commission." },
				new PercentageCost { Name = "Marketing", Percentage = 8, Description = "Campaign and promotion reserve." },
				new PercentageCost { Name = "Waste Allowance", Percentage = 5, Description = "Fabric, thread, and material waste allowance." }
			};

			foreach (var percentageCost in percentageCosts)
			{
				if (!_db.PercentageCosts.Any(existingPercentageCost => existingPercentageCost.Name == percentageCost.Name))
				{
					_db.PercentageCosts.Add(percentageCost);
				}
			}

			_db.SaveChanges();
		}

		private void SeedCalculatorInventory()
		{
			var fabrics = new[]
			{
				new Fabric { Name = "Cotton Canvas", Price = 45000, Quantity = 25, Description = "Durable canvas for bags and structured pieces." },
				new Fabric { Name = "Waterproof Cordura", Price = 78000, Quantity = 20, Description = "Water-resistant technical fabric." },
				new Fabric { Name = "French Terry", Price = 62000, Quantity = 30, Description = "Soft knit for hoodies." },
				new Fabric { Name = "Fleece", Price = 54000, Quantity = 28, Description = "Warm lining and hoodie fabric." },
				new Fabric { Name = "Denim", Price = 66000, Quantity = 22, Description = "Medium-weight denim." },
				new Fabric { Name = "Ripstop Nylon", Price = 73000, Quantity = 24, Description = "Lightweight reinforced nylon." },
				new Fabric { Name = "Twill", Price = 51000, Quantity = 26, Description = "Versatile twill for jackets and totes." },
				new Fabric { Name = "Linen Blend", Price = 48000, Quantity = 18, Description = "Light linen blend for casual bags." },
				new Fabric { Name = "Printed Poplin", Price = 39000, Quantity = 21, Description = "Decorative printed cotton poplin." }
			};

			foreach (var fabric in fabrics)
			{
				if (!_db.Fabrics.Any(existingFabric => existingFabric.Name == fabric.Name))
				{
					_db.Fabrics.Add(fabric);
				}
			}

			var packagings = new[]
			{
				new Packaging { Name = "Small Kraft Box", Price = 18000, Quantity = 100, Description = "Small kraft shipping box." },
				new Packaging { Name = "Medium Kraft Box", Price = 26000, Quantity = 100, Description = "Medium kraft shipping box." },
				new Packaging { Name = "Large Kraft Box", Price = 36000, Quantity = 80, Description = "Large kraft shipping box." },
				new Packaging { Name = "Compostable Mailer", Price = 22000, Quantity = 100, Description = "Flexible compostable mailer." },
				new Packaging { Name = "Poly Mailer", Price = 15000, Quantity = 120, Description = "Lightweight poly mailer." },
				new Packaging { Name = "Tissue Paper", Price = 9000, Quantity = 200, Description = "Wrapping tissue paper." },
				new Packaging { Name = "Sticker Seal", Price = 6000, Quantity = 300, Description = "Branded sealing sticker." },
				new Packaging { Name = "Hang Tag", Price = 8000, Quantity = 250, Description = "Product hang tag." },
				new Packaging { Name = "Dust Bag", Price = 30000, Quantity = 90, Description = "Reusable fabric dust bag." }
			};

			foreach (var packaging in packagings)
			{
				if (!_db.Packagings.Any(existingPackaging => existingPackaging.Name == packaging.Name))
				{
					_db.Packagings.Add(packaging);
				}
			}

			var garmentHardwares = new[]
			{
				new GarmentHardware { Name = "Metal Zipper", Price = 30000, Quantity = 100, Description = "General-purpose metal zipper." },
				new GarmentHardware { Name = "Plastic Zipper", Price = 18000, Quantity = 100, Description = "Lightweight plastic zipper." },
				new GarmentHardware { Name = "Magnetic Snap", Price = 24000, Quantity = 100, Description = "Magnetic snap closure." },
				new GarmentHardware { Name = "D-Ring", Price = 16000, Quantity = 200, Description = "Metal D-ring for straps." },
				new GarmentHardware { Name = "Slider Buckle", Price = 22000, Quantity = 150, Description = "Adjustable strap slider." },
				new GarmentHardware { Name = "Drawcord", Price = 12000, Quantity = 80, Description = "Cotton drawcord." },
				new GarmentHardware { Name = "Eyelet", Price = 9000, Quantity = 300, Description = "Metal eyelets." },
				new GarmentHardware { Name = "Snap Button", Price = 11000, Quantity = 250, Description = "Press snap button." },
				new GarmentHardware { Name = "Webbing Strap", Price = 28000, Quantity = 100, Description = "Durable webbing strap." }
			};

			foreach (var garmentHardware in garmentHardwares)
			{
				if (!_db.GarmentHardwares.Any(existingGarmentHardware => existingGarmentHardware.Name == garmentHardware.Name))
				{
					_db.GarmentHardwares.Add(garmentHardware);
				}
			}

			_db.SaveChanges();
		}

		private void SeedCalculatorAssignments()
		{
			var productIds = _db.Products.ToDictionary(product => product.Name, product => product.Id);
			var categoryIds = _db.Categories.ToDictionary(category => category.Name, category => category.Id);
			var fabricIds = _db.Fabrics.ToDictionary(fabric => fabric.Name, fabric => fabric.Id);
			var packagingIds = _db.Packagings.ToDictionary(packaging => packaging.Name, packaging => packaging.Id);
			var garmentHardwareIds = _db.GarmentHardwares.ToDictionary(garmentHardware => garmentHardware.Name, garmentHardware => garmentHardware.Id);

			AddFabricAssignment(productIds, fabricIds, "Reconnaissance Legion Backpack", "Waterproof Cordura", 0.85m);
			AddFabricAssignment(productIds, fabricIds, "Reconnaissance Legion Backpack", "Printed Poplin", 0.25m);
			AddFabricAssignment(productIds, fabricIds, "Akatsuki Mini Backpack", "Cotton Canvas", 0.70m);
			AddFabricAssignment(productIds, fabricIds, "Akatsuki Mini Backpack", "Printed Poplin", 0.20m);
			AddFabricAssignment(productIds, fabricIds, "Totoro Forest Backpack", "Linen Blend", 0.80m);
			AddFabricAssignment(productIds, fabricIds, "Totoro Forest Backpack", "Fleece", 0.25m);
			AddFabricAssignment(productIds, fabricIds, "Kitsune Phone Holder", "Ripstop Nylon", 0.35m);
			AddFabricAssignment(productIds, fabricIds, "Sakura Phone Holder", "Printed Poplin", 0.30m);
			AddFabricAssignment(productIds, fabricIds, "Karasuno Flight Hoodie", "French Terry", 1.60m);
			AddFabricAssignment(productIds, fabricIds, "Uchiha Crest Hoodie", "Fleece", 1.50m);
			AddFabricAssignment(productIds, fabricIds, "Survey Corps Jacket", "Twill", 1.80m);
			AddFabricAssignment(productIds, fabricIds, "Akira Capsule Jacket", "Denim", 1.90m);
			AddFabricAssignment(productIds, fabricIds, "Spirited Bathhouse Tote", "Cotton Canvas", 0.65m);
			AddFabricAssignment(productIds, fabricIds, "Moon Prism Tote", "Linen Blend", 0.60m);
			AddFabricAssignment(productIds, fabricIds, "Demon Slayer Utility Tote", "Twill", 0.70m);

			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Reconnaissance Legion Backpack", "Metal Zipper", 2);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Reconnaissance Legion Backpack", "Webbing Strap", 2.5m);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Akatsuki Mini Backpack", "Plastic Zipper", 2);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Akatsuki Mini Backpack", "D-Ring", 2);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Totoro Forest Backpack", "Magnetic Snap", 1);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Kitsune Phone Holder", "Slider Buckle", 1);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Sakura Phone Holder", "Magnetic Snap", 1);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Karasuno Flight Hoodie", "Drawcord", 1.5m);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Karasuno Flight Hoodie", "Eyelet", 2);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Uchiha Crest Hoodie", "Drawcord", 1.5m);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Uchiha Crest Hoodie", "Eyelet", 2);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Survey Corps Jacket", "Snap Button", 6);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Akira Capsule Jacket", "Metal Zipper", 3);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Spirited Bathhouse Tote", "Magnetic Snap", 1);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Moon Prism Tote", "D-Ring", 2);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Demon Slayer Utility Tote", "Webbing Strap", 1.5m);

			AddPackagingAssignment(categoryIds, packagingIds, "Backpack", "Large Kraft Box", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Backpack", "Dust Bag", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Phone Holder", "Small Kraft Box", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Phone Holder", "Sticker Seal", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Jacket", "Large Kraft Box", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Jacket", "Hang Tag", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Hoodie", "Medium Kraft Box", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Hoodie", "Tissue Paper", 2);
			AddPackagingAssignment(categoryIds, packagingIds, "Tote Bag", "Compostable Mailer", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Tote Bag", "Poly Mailer", 1);

			_db.SaveChanges();
		}

		private void AddFabricAssignment(Dictionary<string, int> productIds, Dictionary<string, int> fabricIds, string productName, string fabricName, decimal quantity)
		{
			if (!productIds.TryGetValue(productName, out var productId) || !fabricIds.TryGetValue(fabricName, out var fabricId))
			{
				return;
			}

			if (!_db.UnitsFabricByProduct.Any(unit => unit.ProductId == productId && unit.FabricId == fabricId))
			{
				_db.UnitsFabricByProduct.Add(new UnitFabricByProduct { ProductId = productId, FabricId = fabricId, Quantity = quantity });
			}
		}

		private void AddGarmentHardwareAssignment(Dictionary<string, int> productIds, Dictionary<string, int> garmentHardwareIds, string productName, string garmentHardwareName, decimal quantity)
		{
			if (!productIds.TryGetValue(productName, out var productId) || !garmentHardwareIds.TryGetValue(garmentHardwareName, out var garmentHardwareId))
			{
				return;
			}

			if (!_db.UnitsGarmentHardwareByProduct.Any(unit => unit.ProductId == productId && unit.GarmentHardwareId == garmentHardwareId))
			{
				_db.UnitsGarmentHardwareByProduct.Add(new UnitGarmentHardwareByProduct { ProductId = productId, GarmentHardwareId = garmentHardwareId, Quantity = quantity });
			}
		}

		private void AddPackagingAssignment(Dictionary<string, int> categoryIds, Dictionary<string, int> packagingIds, string categoryName, string packagingName, decimal quantity)
		{
			if (!categoryIds.TryGetValue(categoryName, out var categoryId) || !packagingIds.TryGetValue(packagingName, out var packagingId))
			{
				return;
			}

			if (!_db.UnitsPackagingByCategory.Any(unit => unit.CategoryId == categoryId && unit.PackagingId == packagingId))
			{
				_db.UnitsPackagingByCategory.Add(new UnitPackagingByCategory { CategoryId = categoryId, PackagingId = packagingId, Quantity = quantity });
			}
		}

		private void EnsureCalculatorRows()
		{
			var activeProductIds = _db.Products
				.Where(product => !product.IsDeleted)
				.Select(product => product.Id)
				.ToList();

			var activeCategoryIds = _db.Categories
				.Where(category => !category.IsDeleted)
				.Select(category => category.Id)
				.ToList();

			foreach (var productId in activeProductIds)
			{
				if (!_db.FabricsByProduct.Any(fabricByProduct => fabricByProduct.ProductId == productId))
				{
					_db.FabricsByProduct.Add(new FabricByProduct { ProductId = productId });
				}

				if (!_db.GarmentHardwaresByProduct.Any(garmentHardwareByProduct => garmentHardwareByProduct.ProductId == productId))
				{
					_db.GarmentHardwaresByProduct.Add(new GarmentHardwareByProduct { ProductId = productId });
				}
			}

			foreach (var categoryId in activeCategoryIds)
			{
				if (!_db.PackagingsByCategory.Any(packagingByCategory => packagingByCategory.CategoryId == categoryId))
				{
					_db.PackagingsByCategory.Add(new PackagingByCategory { CategoryId = categoryId });
				}
			}

			_db.SaveChanges();
		}

		private void ExecuteIfNotExists(string sql)
		{
			try
			{
				_db.Database.ExecuteSqlRaw(sql);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error executing SQL: {ex.Message}");
			}
		}
	}
}
