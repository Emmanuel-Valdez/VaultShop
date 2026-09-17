using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Models.ViewModels;
using VaultShop.Web.Areas.Admin.Controllers;
using VaultShop.Web.Services.ImageStorage;
using VaultShop.Web.Services.ProductImages;
using VaultShop.Web.Services.RichText;

namespace VaultShop.Web.Tests;

public class ProductControllerUpsertTests
{
    // 2.2 — GET create defaults to 30, edit preserves value
    [Fact]
    public void Upsert_Get_Create_DefaultsTo30()
    {
        var uow = CreateUnitOfWork();
        var controller = CreateController(uow);

        var result = controller.Upsert(null);

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<ProductVM>(view.Model);
        Assert.Equal(30, vm.Product.MaxExpectation);
        Assert.True(vm.Product.IsAvailableInStore);
    }

    [Fact]
    public void Upsert_Get_Edit_PreservesExistingExpectation()
    {
        var existing = new Product { Id = 42, Name = "Existing", Description = "Desc", MaxExpectation = 25, CategoryId = 1, ListPrice = 100m, FinalRetailPrice = 100m, FinalWholesalePrice = 80m, IsDeleted = false };
        var uow = CreateUnitOfWork(existingProduct: existing);
        var controller = CreateController(uow);

        var result = controller.Upsert(42);

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<ProductVM>(view.Model);
        Assert.Equal(25, vm.Product.MaxExpectation);
        Assert.Equal(42, vm.Product.Id);
    }

    // 2.1 — validation: Product.MaxExpectation boundaries via DataAnnotations (correct location, includes -1)
    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(10001, false)]
    [InlineData(1, true)]
    [InlineData(10000, true)]
    public void Product_MaxExpectation_Validation_Theory(int expectation, bool shouldBeValid)
    {
        var product = new Product
        {
            Id = 1,
            Name = "Test",
            Description = "Desc",
            ListPrice = 1000,
            FinalRetailPrice = 1000,
            FinalWholesalePrice = 800,
            MaxExpectation = expectation,
            CategoryId = 1,
            Category = new Category { Id = 1, Name = "Cat", AvgShippingCost = 100m },
            StockQuantity = 10,
            IsAvailableInStore = true,
            IsDeleted = false
        };

        var ctx = new ValidationContext(product);
        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(product, ctx, results, validateAllProperties: true);

        Assert.Equal(shouldBeValid, valid);
        if (!shouldBeValid)
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(Product.MaxExpectation)));
    }

    // 2.1 — controller POST with invalid expectation does not save
    [Theory]
    [InlineData(0)]
    [InlineData(10001)]
    public async Task Upsert_Post_InvalidExpectation_DoesNotSave(int badExpectation)
    {
        var uow = CreateUnitOfWork();
        var controller = CreateController(uow);
        controller.ModelState.AddModelError("Product.MaxExpectation", "invalid");

        var vm = new ProductVM
        {
            Product = new Product
            {
                Id = 0,
                Name = "Test",
                Description = "Desc",
                MaxExpectation = badExpectation,
                CategoryId = 1,
                ListPrice = 100m,
                FinalRetailPrice = 100m,
                FinalWholesalePrice = 80m,
                IsDeleted = false,
                IsAvailableInStore = true
            }
        };

        var result = await controller.Upsert(vm, new List<IFormFile>());

        Assert.IsType<ViewResult>(result);
        uow.ProductMock.Verify(p => p.Add(It.IsAny<Product>()), Times.Never);
        uow.Mock.Verify(u => u.Save(), Times.Never);
    }

    // 2.3 — retail below wholesale is allowed and saves (non-blocking)
    [Fact]
    public async Task Upsert_Post_RetailBelowWholesale_SavesAndRedirects()
    {
        var uow = CreateUnitOfWork();
        var controller = CreateController(uow);

        var vm = new ProductVM
        {
            Product = new Product
            {
                Id = 0,
                Name = "Test",
                Description = "Desc",
                MaxExpectation = 10,
                CategoryId = 1,
                ListPrice = 100m,
                FinalRetailPrice = 900m,
                FinalWholesalePrice = 1000m,
                IsDeleted = false,
                IsAvailableInStore = true
            }
        };

        var result = await controller.Upsert(vm, new List<IFormFile>());

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        uow.ProductMock.Verify(p => p.Add(It.IsAny<Product>()), Times.Once);
        uow.Mock.Verify(u => u.Save(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Upsert_Post_RetailAboveWholesale_SavesAndRedirects()
    {
        var uow = CreateUnitOfWork();
        var controller = CreateController(uow);

        var vm = new ProductVM
        {
            Product = new Product
            {
                Id = 0,
                Name = "Test",
                Description = "Desc",
                MaxExpectation = 10,
                CategoryId = 1,
                ListPrice = 100m,
                FinalRetailPrice = 1200m,
                FinalWholesalePrice = 1000m,
                IsDeleted = false,
                IsAvailableInStore = true
            }
        };

        var result = await controller.Upsert(vm, new List<IFormFile>());

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        uow.Mock.Verify(u => u.Save(), Times.AtLeastOnce);
    }

    // 6.1 — edit page lists active keywords and pre-checks the product's current selection
    [Fact]
    public void Upsert_Get_Edit_PopulatesSelectedKeywordIds()
    {
        var existing = new Product
        {
            Id = 42, Name = "Existing", Description = "Desc", MaxExpectation = 25, CategoryId = 1,
            ListPrice = 100m, FinalRetailPrice = 100m, FinalWholesalePrice = 80m, IsDeleted = false,
            Keywords = new List<ProductKeyword>
            {
                new() { ProductId = 42, KeywordId = 7 },
                new() { ProductId = 42, KeywordId = 9 }
            }
        };
        var uow = CreateUnitOfWork(existingProduct: existing);
        var controller = CreateController(uow);

        var result = controller.Upsert(42);

        var vm = Assert.IsType<ProductVM>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(new[] { 7, 9 }, vm.SelectedKeywordIds);
        Assert.Equal(3, vm.KeywordList.Count);
    }

    // 6.3 — saving a product with multiple keywords persists all new links
    [Fact]
    public async Task Upsert_Post_NewProduct_PersistsSelectedKeywords()
    {
        var uow = CreateUnitOfWork();
        var controller = CreateController(uow);

        var vm = BuildValidVm(id: 0, selectedKeywordIds: new List<int> { 7, 8 });

        var result = await controller.Upsert(vm, new List<IFormFile>());

        Assert.IsType<RedirectToActionResult>(result);
        uow.ProductKeywordMock.Verify(p => p.Add(It.Is<ProductKeyword>(pk => pk.ProductId == vm.Product.Id && pk.KeywordId == 7)), Times.Once);
        uow.ProductKeywordMock.Verify(p => p.Add(It.Is<ProductKeyword>(pk => pk.ProductId == vm.Product.Id && pk.KeywordId == 8)), Times.Once);
        uow.ProductKeywordMock.Verify(p => p.RemoveRange(It.IsAny<IEnumerable<ProductKeyword>>()), Times.Never);
    }

    // 6.3 — unchecking a keyword removes only the extra link
    [Fact]
    public async Task Upsert_Post_UncheckedKeyword_RemovesOnlyThatLink()
    {
        var uow = CreateUnitOfWork(existingLinks: new List<ProductKeyword>
        {
            new() { ProductId = 42, KeywordId = 7 },
            new() { ProductId = 42, KeywordId = 9 }
        });
        var controller = CreateController(uow);
        var vm = BuildValidVm(id: 42, selectedKeywordIds: new List<int> { 7 });

        var result = await controller.Upsert(vm, new List<IFormFile>());

        Assert.IsType<RedirectToActionResult>(result);
        uow.ProductKeywordMock.Verify(p => p.Add(It.IsAny<ProductKeyword>()), Times.Never);
        uow.ProductKeywordMock.Verify(
            p => p.RemoveRange(It.Is<IEnumerable<ProductKeyword>>(links => links.Single().KeywordId == 9)),
            Times.Once);
    }

    // 6.3 — duplicate selection does not create duplicate rows
    [Fact]
    public async Task Upsert_Post_DuplicateSelection_AddsSingleLink()
    {
        var uow = CreateUnitOfWork();
        var controller = CreateController(uow);
        var vm = BuildValidVm(id: 0, selectedKeywordIds: new List<int> { 7, 7 });

        await controller.Upsert(vm, new List<IFormFile>());

        uow.ProductKeywordMock.Verify(p => p.Add(It.Is<ProductKeyword>(pk => pk.KeywordId == 7)), Times.Once);
    }

    // 3.1 — tampered keyword IDs (deleted or non-existent) are filtered out
    [Fact]
    public async Task Upsert_Post_TamperedKeywordId_IgnoresDeletedAndInvalid()
    {
        var uow = CreateUnitOfWork();
        // KeywordMock returns only IDs 7, 8, 9 as active — 999 and 9999 don't exist
        var controller = CreateController(uow);
        var vm = BuildValidVm(id: 0, selectedKeywordIds: new List<int> { 7, 999, 9999 });

        await controller.Upsert(vm, new List<IFormFile>());

        // Only ID 7 should be added (valid active keyword)
        uow.ProductKeywordMock.Verify(p => p.Add(It.Is<ProductKeyword>(pk => pk.KeywordId == 7)), Times.Once);
        uow.ProductKeywordMock.Verify(p => p.Add(It.Is<ProductKeyword>(pk => pk.KeywordId == 999)), Times.Never);
        uow.ProductKeywordMock.Verify(p => p.Add(It.Is<ProductKeyword>(pk => pk.KeywordId == 9999)), Times.Never);
    }

    private static ProductVM BuildValidVm(int id, List<int> selectedKeywordIds) => new()
    {
        Product = new Product
        {
            Id = id,
            Name = "Test",
            Description = "Desc",
            MaxExpectation = 10,
            CategoryId = 1,
            ListPrice = 100m,
            FinalRetailPrice = 1200m,
            FinalWholesalePrice = 1000m,
            IsDeleted = false,
            IsAvailableInStore = true
        },
        SelectedKeywordIds = selectedKeywordIds
    };

    private static ProductController CreateController(TestUow uow)
    {
        var localizer = new Mock<IStringLocalizer<ProductController>>();
        localizer.Setup(x => x[It.IsAny<string>()]).Returns((string n) => new LocalizedString(n, n));
        localizer.Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string n, object[] a) => new LocalizedString(n, n));

        var sanitizer = new Mock<IRichTextSanitizer>();
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string?>())).Returns((string? s) => s);

        var imageService = new Mock<IProductImageService>();
        imageService.Setup(s => s.SaveProductImagesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<IFormFile>?>()))
            .ReturnsAsync(new ProductImageUploadResult());

        var controller = new ProductController(uow.Mock.Object, localizer.Object, imageService.Object, Mock.Of<IImageStorageService>(), sanitizer.Object, NullLogger<ProductController>.Instance)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };
        return controller;
    }

    private static TestUow CreateUnitOfWork(Product? existingProduct = null, List<ProductKeyword>? existingLinks = null)
    {
        var uow = new TestUow();
        uow.CategoryMock.Setup(c => c.GetAll(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .Returns(new List<Category> { new() { Id = 1, Name = "Cat", AvgShippingCost = 100m } });
        uow.KeywordMock.Setup(k => k.GetAll(It.IsAny<Expression<Func<Keyword, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .Returns(new List<Keyword>
            {
                new() { Id = 7, Name = "Naruto" },
                new() { Id = 8, Name = "Berserk" },
                new() { Id = 9, Name = "One Piece" }
            });
        uow.ExistingLinks = existingLinks ?? new List<ProductKeyword>();
        uow.ProductKeywordMock
            .Setup(x => x.GetAll(It.IsAny<Expression<Func<ProductKeyword, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .Returns((Expression<Func<ProductKeyword, bool>>? filter, string? _, bool __) =>
                uow.ExistingLinks.Where(filter?.Compile() ?? (_ => true)).ToList());
        if (existingProduct != null)
        {
            uow.ProductMock.Setup(p => p.Get(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
                .Returns(existingProduct);
        }
        return uow;
    }

    private sealed class TestUow
    {
        public Mock<IUnitOfWork> Mock { get; } = new();
        public Mock<ICategoryRepository> CategoryMock { get; } = new();
        public Mock<IProductRepository> ProductMock { get; } = new();
        public Mock<IProductImageRepository> ProductImageMock { get; } = new();
        public Mock<IKeywordRepository> KeywordMock { get; } = new();
        public Mock<IProductKeywordRepository> ProductKeywordMock { get; } = new();
        public List<ProductKeyword> ExistingLinks { get; set; } = new();
        public TestUow()
        {
            Mock.Setup(u => u.Category).Returns(CategoryMock.Object);
            Mock.Setup(u => u.Product).Returns(ProductMock.Object);
            Mock.Setup(u => u.ProductImage).Returns(ProductImageMock.Object);
            Mock.Setup(u => u.Keyword).Returns(KeywordMock.Object);
            Mock.Setup(u => u.ProductKeyword).Returns(ProductKeywordMock.Object);
        }
    }
}
