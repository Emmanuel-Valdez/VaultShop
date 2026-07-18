using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stripe.Checkout;
using VaultShop.DataAccess.Data;
using VaultShop.Models;
using VaultShop.Utility;
using VaultShop.Web.Services.Payments;

namespace VaultShop.Web.Tests
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private SqliteConnection? _connection;

        // Populated during ConfigureWebHost so tests can reference the seeded fixtures by id/email.
        public string CustomerEmail => TestDataSeeder.CustomerEmail;
        public string CompanyEmail => TestDataSeeder.CompanyEmail;
        public string TestPassword => TestDataSeeder.Password;
        public int TestCompanyId { get; private set; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Stripe:SecretKey"] = "sk_test_dummy",
                    ["Database:RunMigrationsOnStartup"] = "false",
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                _connection = new SqliteConnection("Data Source=:memory:");
                _connection.Open();
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));

                services.RemoveAll<IStripeCheckoutSessionClient>();
                services.AddScoped<IStripeCheckoutSessionClient, FakeStripeCheckoutSessionClient>();

                using var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Database.EnsureCreated();
                TestCompanyId = TestDataSeeder.SeedRolesAndUsers(scope.ServiceProvider).GetAwaiter().GetResult();
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _connection?.Dispose();
            }
        }

        // Mirrors CapturingStripeCheckoutSessionClient in StripePaymentSessionServiceTests.cs,
        // made public/reusable instead of a private nested class, since multiple new test
        // files need the same fake.
        private sealed class FakeStripeCheckoutSessionClient : IStripeCheckoutSessionClient
        {
            public Session Create(SessionCreateOptions options) => new()
            {
                Id = "cs_test_123",
                PaymentIntentId = "pi_test_123",
                Url = "https://stripe.test/checkout",
            };

            public Session Get(string sessionId) => new() { Id = sessionId, PaymentStatus = "unpaid" };

            public Session Expire(string sessionId) => new() { Id = sessionId };
        }
    }

    internal static class TestDataSeeder
    {
        public const string CustomerEmail = "customer.tests@vaultshop.local";
        public const string CompanyEmail = "company.tests@vaultshop.local";
        public const string Password = "Test123!";

        // Returns the seeded Company's Id so tests can assert OrderHeader.CompanyId.
        public static async Task<int> SeedRolesAndUsers(IServiceProvider services)
        {
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in new[] { SD.Role_Customer, SD.Role_Company, SD.Role_Admin, SD.Role_Employee })
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            var db = services.GetRequiredService<ApplicationDbContext>();
            var company = new Company { Name = "Test Company", IsDeleted = false };
            db.Companies.Add(company);
            await db.SaveChangesAsync();

            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            var customer = new ApplicationUser
            {
                UserName = CustomerEmail,
                Email = CustomerEmail,
                Name = "Test Customer",
                EmailConfirmed = true,
            };
            await userManager.CreateAsync(customer, Password);
            await userManager.AddToRoleAsync(customer, SD.Role_Customer);

            var companyUser = new ApplicationUser
            {
                UserName = CompanyEmail,
                Email = CompanyEmail,
                Name = "Test Company User",
                EmailConfirmed = true,
                CompanyId = company.Id,
            };
            await userManager.CreateAsync(companyUser, Password);
            await userManager.AddToRoleAsync(companyUser, SD.Role_Company);

            return company.Id;
        }
    }
}
