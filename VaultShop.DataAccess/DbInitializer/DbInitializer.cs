using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VaultShop.DataAccess.Data;
using VaultShop.Models;
using VaultShop.Models.CalculatorModels;
using VaultShop.Utility;

namespace VaultShop.DataAccess.DbInitializer
{
	public class DbInitializer : IDbInitializer
	{
		private readonly UserManager<ApplicationUser> _userManager;
		private readonly RoleManager<IdentityRole> _roleManager;
		private readonly ApplicationDbContext _db;
		private readonly IConfiguration _configuration;
		private readonly ILogger<DbInitializer> _logger;

		public DbInitializer(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
			ApplicationDbContext db, IConfiguration configuration, ILogger<DbInitializer> logger)
		{
			_roleManager = roleManager;
			_userManager = userManager;
			_db = db;
			_configuration = configuration;
			_logger = logger;
		}

		public void Initialize()
		{
			try
			{
				if (_db.Database.GetPendingMigrations().Count() > 0)
				{
					_db.Database.Migrate();
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to apply pending migrations or create database views/triggers during startup initialization.");
			}

			EnsureRequiredRoles();
			EnsureAdminUser();
			EnsurePercentageProfit();
		}

		private void EnsureRequiredRoles()
		{
			var roles = new[] { SD.Role_Customer, SD.Role_Employee, SD.Role_Admin, SD.Role_Company };

			foreach (var role in roles)
			{
				if (!_roleManager.RoleExistsAsync(role).GetAwaiter().GetResult())
				{
					_roleManager.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult();
				}
			}
		}

		private void EnsureAdminUser()
		{
			var adminEmail = _configuration["Seed:AdminEmail"]
				?? Environment.GetEnvironmentVariable("Seed__AdminEmail");
			var adminPassword = _configuration["Seed:AdminPassword"]
				?? Environment.GetEnvironmentVariable("Seed__AdminPassword");

			if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
			{
				throw new InvalidOperationException("Missing required credentials for admin seed. " +
					"Please provide Seed:AdminEmail and Seed:AdminPassword (or the environment variables Seed__AdminEmail / Seed__AdminPassword).");
			}

			var adminUser = _db.ApplicationUsers.FirstOrDefault(user => user.Email == adminEmail);
			if (adminUser == null)
			{
				adminUser = new ApplicationUser
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
					var errors = string.Join(", ", createResult.Errors.Select(error => error.Description));
					throw new InvalidOperationException("Failed to create admin user: " + errors);
				}
			}

			if (!_userManager.IsInRoleAsync(adminUser, SD.Role_Admin).GetAwaiter().GetResult())
			{
				_userManager.AddToRoleAsync(adminUser, SD.Role_Admin).GetAwaiter().GetResult();
			}
		}

		private void EnsurePercentageProfit()
		{
			if (!_db.PercentageProfits.Any())
			{
				_db.PercentageProfits.Add(new PercentageProfit
				{
					Retail = 0,
					Wholesale = 0
				});
				_db.SaveChanges();
			}
		}
	}
}

