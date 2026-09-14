using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VaultShop.DataAccess.Data;
using VaultShop.Models;
using VaultShop.Models.CalculatorModels;
using VaultShop.Web.Services.Pricing;

namespace VaultShop.Web.Tests;

public class PricingCalculatorServiceGuardTests
{
    [Fact]
    public void GetCostByProducts_WithZeroExpectation_ThrowsInvalidOperation()
    {
        using var connection = CreateOpenConnection();
        var options = CreateOptions(connection);
        EnsureDatabaseCreated(options);

        using (var context = new ApplicationDbContext(options))
        {
            var category = new Category { Name = "Cat", AvgShippingCost = 0m };
            context.Categories.Add(category);
            // bypass validation via raw insert with 0
            context.Products.Add(new Product { Name = "Zero", Description = "x", MaxExpectation = 0, Category = category, ListPrice = 100m, FinalRetailPrice = 100m, FinalWholesalePrice = 100m, IsDeleted = false });
            context.FixedCosts.Add(new FixedCost { Name = "FC", Cost = 1000m });
            context.SaveChanges();
        }

        using var verificationContext = new ApplicationDbContext(options);
        var service = new PricingCalculatorService(verificationContext);

        var ex = Assert.Throws<InvalidOperationException>(() => service.GetCostByProducts());
        Assert.Contains("MaxExpectation", ex.Message);
        Assert.Contains("0", ex.Message);
    }

    [Fact]
    public void GetCostByProducts_WithNegativeExpectation_ThrowsInvalidOperation()
    {
        using var connection = CreateOpenConnection();
        var options = CreateOptions(connection);
        EnsureDatabaseCreated(options);

        using (var context = new ApplicationDbContext(options))
        {
            var category = new Category { Name = "Cat", AvgShippingCost = 0m };
            context.Categories.Add(category);
            context.Products.Add(new Product { Name = "Neg", Description = "x", MaxExpectation = -5, Category = category, ListPrice = 100m, FinalRetailPrice = 100m, FinalWholesalePrice = 100m, IsDeleted = false });
            context.FixedCosts.Add(new FixedCost { Name = "FC", Cost = 1000m });
            context.SaveChanges();
        }

        using var verificationContext = new ApplicationDbContext(options);
        var service = new PricingCalculatorService(verificationContext);

        Assert.Throws<InvalidOperationException>(() => service.GetCostByProducts());
    }

    private static SqliteConnection CreateOpenConnection()
    {
        var c = new SqliteConnection("Data Source=:memory:");
        c.Open();
        return c;
    }

    private static DbContextOptions<ApplicationDbContext> CreateOptions(SqliteConnection c) => new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(c).Options;

    private static void EnsureDatabaseCreated(DbContextOptions<ApplicationDbContext> o)
    {
        using var ctx = new ApplicationDbContext(o);
        ctx.Database.EnsureCreated();
    }
}
