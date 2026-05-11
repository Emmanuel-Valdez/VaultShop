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
				SeedDemoData();
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

				ApplicationUser user = _db.ApplicationUsers.FirstOrDefault(u => u.Email == adminEmail);
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
			if (!_db.Categories.Any())
			{
				_db.Categories.AddRange(
					new Category {  Name = "Backpack", MaxExpectation = 40, AvgShippingCost = 10000 },
					new Category {  Name = "PhoneHolder", MaxExpectation = 150, AvgShippingCost = 3500 },
					new Category {  Name = "Jacket", MaxExpectation = 80, AvgShippingCost = 5000 }
				);
				_db.SaveChanges();
			}

			if (!_db.Companies.Any())
			{
				_db.Companies.AddRange(
					new Company {  Name = "Demo Company 1", StreetAddress = "Demo Street 1", State = "Demo State", City = "Demo City", PostalCode = "12345", PhoneNumber = "1234567890" },
					new Company { Name = "Demo Company 2", StreetAddress = "Demo Street 2", State = "Demo State", City = "Demo City", PostalCode = "12345", PhoneNumber = "0987654321" },
					new Company { Name = "Demo Company 3", StreetAddress = "Demo Street 3", State = "Demo State", City = "Demo City", PostalCode = "12345", PhoneNumber = "1122334455" }
				);
				_db.SaveChanges();
			}

			if (!_db.Products.Any())
			{
				_db.Products.AddRange(
					new Product { Name = "Reconnaissance Legion", Description = "BackPack inspired in the reconnaissance legion of Attack On Titan", FinalRetailPrice = 34900, FinalWholesalePrice = 18390, CategoryId = 1 },
					new Product { Name = "Akkatsuki Little Bag", Description = "Little Bag inspired in the criminal organization of shinoby in Naruto series", FinalRetailPrice = 44900, FinalWholesalePrice = 26567, CategoryId = 2 },
					new Product { Name = "Haikyu Hoodie", Description = "Hooded sweatshirt inspired in the Karasuno Team on Haikyu", FinalRetailPrice = 43234, FinalWholesalePrice = 30060.78m, CategoryId = 3 }
				);
				_db.SaveChanges();

				_db.GarmentHardwaresByProduct.AddRange(
					new GarmentHardwareByProduct { ProductId = 1 },
					new GarmentHardwareByProduct { ProductId = 2 },
					new GarmentHardwareByProduct { ProductId = 3 }
				);
				_db.FabricsByProduct.AddRange(
					new FabricByProduct {  ProductId = 1 },
					new FabricByProduct {  ProductId = 2 },
					new FabricByProduct {  ProductId = 3 }
				);
				_db.PackagingsByCategory.AddRange(
					new PackagingByCategory {  CategoryId = 1 },
					new PackagingByCategory {  CategoryId = 2 },
					new PackagingByCategory {  CategoryId = 3 }
				);
				_db.SaveChanges();
			}
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
