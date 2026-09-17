using VaultShop.Models;
using VaultShop.Models.ViewModels;

namespace VaultShop.Web.Tests;

public class HomeCollectionsTests
{
    [Fact]
    public void ComputeCollections_CountsDistinctAvailableInStockProductsPerKeyword()
    {
        var keyword = new Keyword { Id = 1, Name = "Naruto", Slug = "naruto", IsDeleted = false };
        var products = Enumerable.Range(1, 26)
            .Select(i => new Product
            {
                Id = i,
                Name = $"Product {i}",
                StockQuantity = i > 24 ? 0 : 1,
                IsDeleted = false,
                IsAvailableInStore = true,
                Keywords = [new ProductKeyword { KeywordId = keyword.Id, Keyword = keyword }]
            })
            .ToList();

        var collections = HomeIndexVM.ComputeCollections(products);

        var chip = Assert.Single(collections);
        Assert.Equal(24, chip.Count);
    }

    [Fact]
    public void ComputeCollections_ExcludesSoftDeletedKeywords()
    {
        var active = new Keyword { Id = 1, Name = "Active", Slug = "active", IsDeleted = false };
        var deleted = new Keyword { Id = 2, Name = "Deleted", Slug = "deleted", IsDeleted = true };
        var products = new List<Product>
        {
            NewProduct(1, active),
            NewProduct(2, deleted),
        };

        var collections = HomeIndexVM.ComputeCollections(products);

        var chip = Assert.Single(collections);
        Assert.Equal(1, chip.Id);
        Assert.Equal("Active", chip.Name);
    }

    [Fact]
    public void ComputeCollections_ReturnsChipImageUrlAndOrdersByName()
    {
        var keywordB = new Keyword
        {
            Id = 2,
            Name = "Zeta",
            Slug = "zeta",
            IsDeleted = false,
            Images = [new KeywordImage { Kind = KeywordImageKind.Chip, ImageUrl = "/img/zeta-chip.jpg" }]
        };
        var keywordA = new Keyword { Id = 1, Name = "Alpha", Slug = "alpha", IsDeleted = false };
        var products = new List<Product>
        {
            NewProduct(1, keywordB),
            NewProduct(2, keywordA),
        };

        var collections = HomeIndexVM.ComputeCollections(products);

        Assert.Equal(["Alpha", "Zeta"], collections.Select(c => c.Name));
        Assert.Equal("/img/zeta-chip.jpg", collections.Last().ChipImageUrl);
        Assert.Null(collections.First().ChipImageUrl);
    }

    private static Product NewProduct(int id, Keyword keyword) => new()
    {
        Id = id,
        Name = $"Product {id}",
        StockQuantity = 1,
        IsDeleted = false,
        IsAvailableInStore = true,
        Keywords = [new ProductKeyword { KeywordId = keyword.Id, Keyword = keyword }]
    };
}