using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VaultShop.DataAccess.Data;
using VaultShop.Models;

namespace VaultShop.Web.Tests;

public class CategoryPillHttpTests
{
    [Fact]
    public async Task HomeIndex_RendersPillThumbForImaged_AndFallbackLetterForUnimaged()
    {
        using var factory = new CustomWebApplicationFactory();
        SeedCategories(factory);
        var client = factory.CreateClient();

        var body = await client.GetStringAsync("/en-US/Customer/Home/Index");

        Assert.Contains("category-pill", body);
        Assert.Contains("category-pill__thumb", body);
        Assert.Contains("category-pill__fallback", body);
        Assert.Contains(">A</span>", body); // Accessories fallback letter
        Assert.Contains(">#</span>", body); // 3x2 leads with a digit -> "#"
        // image thumb must be decorative for screen readers
        Assert.Contains("aria-hidden=\"true\"", body);
    }

    [Fact]
    public async Task Search_ActiveCategoryPill_MarksCurrent()
    {
        using var factory = new CustomWebApplicationFactory();
        var (categoryId, _) = SeedCategories(factory);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/en-US/Customer/Home/Search?categoryId={categoryId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("aria-current=\"true\"", body);
        Assert.Contains("btn-primary", body);
    }

    private static (int categoryId, int accessoriesId) SeedCategories(CustomWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var backpacks = new Category
        {
            Name = "Backpacks",
            AvgShippingCost = 100m,
            ImageUrl = "\\images\\categories\\category-1\\backpacks.jpg",
            ObjectKey = "images/categories/category-1/backpacks.jpg",
            FileName = "backpacks.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 100,
            StorageProvider = "LocalFileSystem",
        };
        var accessories = new Category { Name = "Accessories", AvgShippingCost = 100m };
        var numeric = new Category { Name = "3x2 Sets", AvgShippingCost = 100m };
        db.Categories.AddRange(backpacks, accessories, numeric);
        db.SaveChanges();

        db.Products.AddRange(
            new Product
            {
                Name = "City Backpack",
                Description = "Backpack",
                MaxExpectation = 10,
                CategoryId = backpacks.Id,
                ListPrice = 100m,
                FinalRetailPrice = 100m,
                FinalWholesalePrice = 100m,
                IsAvailableInStore = true,
                IsDeleted = false,
                StockQuantity = 3,
            },
            new Product
            {
                Name = "Leather Belt",
                Description = "Acc",
                MaxExpectation = 10,
                CategoryId = accessories.Id,
                ListPrice = 100m,
                FinalRetailPrice = 100m,
                FinalWholesalePrice = 100m,
                IsAvailableInStore = true,
                IsDeleted = false,
                StockQuantity = 3,
            },
            new Product
            {
                Name = "Set of three",
                Description = "Set",
                MaxExpectation = 10,
                CategoryId = numeric.Id,
                ListPrice = 100m,
                FinalRetailPrice = 100m,
                FinalWholesalePrice = 100m,
                IsAvailableInStore = true,
                IsDeleted = false,
                StockQuantity = 3,
            });
        db.SaveChanges();

        return (backpacks.Id, accessories.Id);
    }
}