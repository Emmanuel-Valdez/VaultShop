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
				new Category { Name = "Medium Sailor Bag", MaxExpectation = 80, AvgShippingCost = 5000 },
				new Category { Name = "Medium Ita Backpack", MaxExpectation = 45, AvgShippingCost = 8500 },
				new Category { Name = "Large Backpack", MaxExpectation = 40, AvgShippingCost = 10000 },
				new Category { Name = "Medium Faux Leather Backpack", MaxExpectation = 45, AvgShippingCost = 9000 },
				new Category { Name = "Mochi-Bando", MaxExpectation = 70, AvgShippingCost = 6500 },
				new Category { Name = "Small Crossbody Bag", MaxExpectation = 100, AvgShippingCost = 4000 },
				new Category { Name = "Printed T-Shirt", MaxExpectation = 120, AvgShippingCost = 3500 },
				new Category { Name = "Large Mate Bag", MaxExpectation = 55, AvgShippingCost = 8500 },
				new Category { Name = "Medium Messenger Bag", MaxExpectation = 75, AvgShippingCost = 6000 }
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
				new Product { Name = "Akatsuki Sailor Bag", Description = "Medium sailor bag with a dark Naruto Shippuden look, deep black panels, red Akatsuki cloud accents, and a bold anime style made for everyday use.", FinalRetailPrice = 34900, FinalWholesalePrice = 18390, CategoryId = categoryIds["Medium Sailor Bag"], IsAvailableInStore = true },
				new Product { Name = "Kero Ita Backpack", Description = "Medium ita backpack inspired by Cardcaptor Sakura and Kero, with a cute display window, warm yellow details, soft pastel tones, and playful anime charm.", FinalRetailPrice = 44900, FinalWholesalePrice = 26567, CategoryId = categoryIds["Medium Ita Backpack"], IsAvailableInStore = true },
				new Product { Name = "EVA-01 Large Backpack", Description = "Large Evangelion EVA 01 backpack with sharp mecha energy, purple and neon green contrast, black details, and a strong Unit 01 anime silhouette.", FinalRetailPrice = 38900, FinalWholesalePrice = 21400, CategoryId = categoryIds["Large Backpack"], IsAvailableInStore = true },
				new Product { Name = "Slytherin Faux Leather Backpack", Description = "Medium faux leather backpack inspired by Harry Potter and Slytherin, styled with deep green, silver accents, a polished wizard look, and house crest energy.", FinalRetailPrice = 21900, FinalWholesalePrice = 12800, CategoryId = categoryIds["Medium Faux Leather Backpack"], IsAvailableInStore = true },
				new Product { Name = "Sakura Mochi-Bando", Description = "Convertible mochi-bando backpack and crossbody bag inspired by Cardcaptor Sakura, with soft pink tones, magical girl details, and a sweet everyday anime style.", FinalRetailPrice = 19900, FinalWholesalePrice = 11600, CategoryId = categoryIds["Mochi-Bando"], IsAvailableInStore = true },
				new Product { Name = "Gaara Small Crossbody Bag", Description = "Small crossbody bag inspired by Gaara from Naruto, with sandy earth colors, dark red details, compact shape, and a strong shinobi anime mood.", FinalRetailPrice = 43234, FinalWholesalePrice = 30060.78m, CategoryId = categoryIds["Small Crossbody Bag"], IsAvailableInStore = true },
				new Product { Name = "EVA-01 Classic T-Shirt", Description = "Classic printed T-shirt inspired by Evangelion Unit 01, featuring a clean anime graphic style with purple, green, and black EVA 01 mecha colors.", FinalRetailPrice = 47900, FinalWholesalePrice = 28900, CategoryId = categoryIds["Printed T-Shirt"], IsAvailableInStore = true },
				new Product { Name = "Tomioka Mochi-Bando", Description = "Convertible mochi-bando backpack and crossbody bag inspired by Giyu Tomioka from Demon Slayer Kimetsu no Yaiba, with water-blue tones, dark contrast, and haori-style pattern details.", FinalRetailPrice = 68900, FinalWholesalePrice = 41200, CategoryId = categoryIds["Mochi-Bando"], IsAvailableInStore = true },
				new Product { Name = "Gondor Large Mate Bag", Description = "Large mate bag inspired by Gondor from The Lord of the Rings, with a noble black and white fantasy palette, silver details, and travel-ready LOTR style.", FinalRetailPrice = 74500, FinalWholesalePrice = 45800, CategoryId = categoryIds["Large Mate Bag"], IsAvailableInStore = true },
				new Product { Name = "One Ring Messenger Bag", Description = "Medium messenger bag inspired by the One Ring from The Lord of the Rings, with warm golden accents, dark fantasy contrast, and a practical LOTR shoulder-bag shape.", FinalRetailPrice = 24900, FinalWholesalePrice = 14800, CategoryId = categoryIds["Medium Messenger Bag"], IsAvailableInStore = true },
				new Product { Name = "Slytherin Large Mate Bag", Description = "Large mate bag inspired by Harry Potter and Slytherin, combining deep green, black, and silver details with a polished wizard-house style.", FinalRetailPrice = 23900, FinalWholesalePrice = 13900, CategoryId = categoryIds["Large Mate Bag"], IsAvailableInStore = true },
				new Product { Name = "Sailor Moon Mochi-Bando", Description = "Convertible mochi-bando backpack and crossbody bag inspired by Sailor Moon, with moon-prism magic, soft pink and blue tones, golden accents, and magical girl sparkle.", FinalRetailPrice = 26900, FinalWholesalePrice = 15900, CategoryId = categoryIds["Mochi-Bando"], IsAvailableInStore = true }
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

			AddFabricAssignment(productIds, fabricIds, "Akatsuki Sailor Bag", "Waterproof Cordura", 0.85m);
			AddFabricAssignment(productIds, fabricIds, "Akatsuki Sailor Bag", "Printed Poplin", 0.25m);
			AddFabricAssignment(productIds, fabricIds, "Kero Ita Backpack", "Cotton Canvas", 0.70m);
			AddFabricAssignment(productIds, fabricIds, "Kero Ita Backpack", "Printed Poplin", 0.20m);
			AddFabricAssignment(productIds, fabricIds, "EVA-01 Large Backpack", "Linen Blend", 0.80m);
			AddFabricAssignment(productIds, fabricIds, "EVA-01 Large Backpack", "Fleece", 0.25m);
			AddFabricAssignment(productIds, fabricIds, "Slytherin Faux Leather Backpack", "Ripstop Nylon", 0.35m);
			AddFabricAssignment(productIds, fabricIds, "Sakura Mochi-Bando", "Printed Poplin", 0.30m);
			AddFabricAssignment(productIds, fabricIds, "Gaara Small Crossbody Bag", "French Terry", 1.60m);
			AddFabricAssignment(productIds, fabricIds, "EVA-01 Classic T-Shirt", "Fleece", 1.50m);
			AddFabricAssignment(productIds, fabricIds, "Tomioka Mochi-Bando", "Twill", 1.80m);
			AddFabricAssignment(productIds, fabricIds, "Gondor Large Mate Bag", "Denim", 1.90m);
			AddFabricAssignment(productIds, fabricIds, "One Ring Messenger Bag", "Cotton Canvas", 0.65m);
			AddFabricAssignment(productIds, fabricIds, "Slytherin Large Mate Bag", "Linen Blend", 0.60m);
			AddFabricAssignment(productIds, fabricIds, "Sailor Moon Mochi-Bando", "Twill", 0.70m);

			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Akatsuki Sailor Bag", "Metal Zipper", 2);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Akatsuki Sailor Bag", "Webbing Strap", 2.5m);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Kero Ita Backpack", "Plastic Zipper", 2);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Kero Ita Backpack", "D-Ring", 2);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "EVA-01 Large Backpack", "Magnetic Snap", 1);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Slytherin Faux Leather Backpack", "Slider Buckle", 1);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Sakura Mochi-Bando", "Magnetic Snap", 1);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Gaara Small Crossbody Bag", "Drawcord", 1.5m);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Gaara Small Crossbody Bag", "Eyelet", 2);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "EVA-01 Classic T-Shirt", "Drawcord", 1.5m);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "EVA-01 Classic T-Shirt", "Eyelet", 2);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Tomioka Mochi-Bando", "Snap Button", 6);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Gondor Large Mate Bag", "Metal Zipper", 3);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "One Ring Messenger Bag", "Magnetic Snap", 1);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Slytherin Large Mate Bag", "D-Ring", 2);
			AddGarmentHardwareAssignment(productIds, garmentHardwareIds, "Sailor Moon Mochi-Bando", "Webbing Strap", 1.5m);

			AddPackagingAssignment(categoryIds, packagingIds, "Medium Sailor Bag", "Medium Kraft Box", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Medium Sailor Bag", "Tissue Paper", 2);
			AddPackagingAssignment(categoryIds, packagingIds, "Medium Ita Backpack", "Large Kraft Box", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Medium Ita Backpack", "Dust Bag", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Large Backpack", "Large Kraft Box", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Large Backpack", "Dust Bag", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Medium Faux Leather Backpack", "Large Kraft Box", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Medium Faux Leather Backpack", "Dust Bag", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Mochi-Bando", "Medium Kraft Box", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Mochi-Bando", "Sticker Seal", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Small Crossbody Bag", "Small Kraft Box", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Small Crossbody Bag", "Sticker Seal", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Printed T-Shirt", "Compostable Mailer", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Printed T-Shirt", "Hang Tag", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Large Mate Bag", "Large Kraft Box", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Large Mate Bag", "Dust Bag", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Medium Messenger Bag", "Medium Kraft Box", 1);
			AddPackagingAssignment(categoryIds, packagingIds, "Medium Messenger Bag", "Hang Tag", 1);

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
