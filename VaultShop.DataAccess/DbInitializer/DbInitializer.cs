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
			EnsurePostalAgencies();
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

		private void EnsurePostalAgencies()
		{
			try
			{
				// ponytail: idempotent upsert by Code; rows with Source="correo" carry official coords from tools/refresh_sucursales.py, LastVerifiedUtc=null means never confirmed (closure candidate)
				var jsonPath = ResolveSucursalesPath();
				if (jsonPath == null)
				{
					_logger.LogWarning("PostalAgency seed skipped — sucursales.json not found.");
					return;
				}
				var json = File.ReadAllText(jsonPath);
				var agencies = System.Text.Json.JsonSerializer.Deserialize<List<PostalAgency>>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
				if (agencies == null || agencies.Count == 0) return;
				// ponytail: sucursales.json carries offsets (Kind=Local) and Npgsql rejects Local for timestamptz — normalize once, covers insert + update paths.
				foreach (var a in agencies)
				{
					if (a.LastVerifiedUtc.HasValue)
					{
						a.LastVerifiedUtc = a.LastVerifiedUtc.Value.ToUniversalTime();
					}
				}
				var existing = _db.PostalAgencies.Select(a => a.Code).ToHashSet();
				var toAdd = agencies.Where(a => !existing.Contains(a.Code)).ToList();
				if (toAdd.Count > 0)
				{
					_db.PostalAgencies.AddRange(toAdd);
					_db.SaveChanges();
					_logger.LogInformation("Seeded {Count} PostalAgencies.", toAdd.Count);
				}
				// update changed rows on reseed (idempotent)
				var changed = 0;
				foreach (var a in agencies.Where(a => existing.Contains(a.Code)))
				{
					var e = _db.PostalAgencies.Find(a.Code);
				if (e != null && (e.Name != a.Name || e.Latitude != a.Latitude || e.Longitude != a.Longitude || e.Street != a.Street || e.PostalCode != a.PostalCode || e.Province != a.Province || e.LastVerifiedUtc != a.LastVerifiedUtc || e.Source != a.Source || e.Services != a.Services || e.Kind != a.Kind))
				{
					e.Name = a.Name; e.Street = a.Street; e.Number = a.Number; e.Locality = a.Locality; e.City = a.City;
					e.Province = a.Province; e.ProvinceCode = a.ProvinceCode; e.PostalCode = a.PostalCode; e.Latitude = a.Latitude; e.Longitude = a.Longitude;
					e.LastVerifiedUtc = a.LastVerifiedUtc; e.Source = a.Source; e.Services = a.Services; e.Kind = a.Kind;
					changed++;
				}
				}
				if (changed > 0) _db.SaveChanges();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to seed PostalAgencies.");
			}
		}

		private string? ResolveSucursalesPath()
		{
			var candidates = new List<string>();
			var baseDir = AppContext.BaseDirectory;
			candidates.Add(Path.Combine(baseDir, "SeedData", "sucursales.json"));
			try
			{
				var asmLoc = typeof(VaultShop.DataAccess.Data.ApplicationDbContext).Assembly.Location;
				if (!string.IsNullOrEmpty(asmLoc))
					candidates.Add(Path.Combine(Path.GetDirectoryName(asmLoc)!, "SeedData", "sucursales.json"));
			}
			catch { }
			candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "VaultShop.DataAccess", "SeedData", "sucursales.json")));
			candidates.Add("VaultShop.DataAccess/SeedData/sucursales.json");
			return candidates.FirstOrDefault(File.Exists);
		}
	}
}

