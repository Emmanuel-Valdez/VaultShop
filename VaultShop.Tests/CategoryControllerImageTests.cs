using System.Linq.Expressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using SkiaSharp;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Models.CalculatorModels;
using VaultShop.Web.Areas.Admin.Controllers;
using VaultShop.Web.Services.CategoryImages;
using VaultShop.Web.Services.ImageStorage;

namespace VaultShop.Web.Tests;

public class CategoryControllerImageTests
{
	[Fact]
	public async Task Upsert_WithValidImage_SavesImageColumnsAndFile()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-category-ctrl-tests-").FullName;
		try
		{
			var category = new Category { Id = 1, Name = "Shoes", AvgShippingCost = 10, IsDeleted = false, PackagingByCategory = new() };
			var uow = new TestUnitOfWork(category);
			var controller = CreateController(uow, webRootPath);

			var png = CreateValidPngBytes();
			var result = await controller.Upsert(category, CreateFormFile(png, "cat.png", "image/png"));

			Assert.IsType<RedirectToActionResult>(result);
			Assert.NotNull(category.ImageUrl);
			Assert.NotNull(category.ObjectKey);
			Assert.StartsWith("images/categories/category-1/", category.ObjectKey);
			Assert.Equal("image/jpeg", category.ContentType);
			Assert.True(category.SizeBytes > 0);
			Assert.Equal(LocalImageStorageService.ProviderName, category.StorageProvider);

			var savedPath = Path.Combine(webRootPath, category.ObjectKey!.Replace('/', Path.DirectorySeparatorChar));
			Assert.True(File.Exists(savedPath));
			using var saved = SKBitmap.Decode(savedPath);
			Assert.Equal(400, saved.Width);
			Assert.Equal(400, saved.Height);

			// exactly one DB save for the category + one for the image columns
			uow.Mock.Verify(x => x.Save(), Times.Exactly(2));
		}
		finally { Directory.Delete(webRootPath, recursive: true); }
	}

	[Fact]
	public async Task Upsert_ReplaceImage_SavesNewThenDeletesOld()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-category-ctrl-tests-").FullName;
		try
		{
			var category = new Category { Id = 2, Name = "Bags", AvgShippingCost = 20, IsDeleted = false, PackagingByCategory = new() };
			var uow = new TestUnitOfWork(category);
			var controller = CreateController(uow, webRootPath);
			var png = CreateValidPngBytes();

			await controller.Upsert(category, CreateFormFile(png, "first.png", "image/png"));
			var firstKey = category.ObjectKey!;
			var firstPath = Path.Combine(webRootPath, firstKey.Replace('/', Path.DirectorySeparatorChar));
			Assert.True(File.Exists(firstPath));

			// second upload replaces
			await controller.Upsert(category, CreateFormFile(png, "second.png", "image/png"));
			var secondKey = category.ObjectKey!;
			Assert.NotEqual(firstKey, secondKey);
			Assert.False(File.Exists(firstPath));
			Assert.True(File.Exists(Path.Combine(webRootPath, secondKey.Replace('/', Path.DirectorySeparatorChar))));

			// two DB saves per upsert (category + image), total four
			uow.Mock.Verify(x => x.Save(), Times.Exactly(4));
		}
		finally { Directory.Delete(webRootPath, recursive: true); }
	}

	[Fact]
	public async Task Upsert_ReplaceImage_WhenNewSaveFails_KeepsOldImageAndStorageUntouched()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-category-ctrl-tests-").FullName;
		try
		{
			var category = new Category { Id = 3, Name = "Hats", AvgShippingCost = 5, IsDeleted = false, PackagingByCategory = new() };
			var uow = new TestUnitOfWork(category);
			var controller = CreateController(uow, webRootPath);
			var png = CreateValidPngBytes();

			await controller.Upsert(category, CreateFormFile(png, "ok.png", "image/png"));
			var oldKey = category.ObjectKey!;
			var oldPath = Path.Combine(webRootPath, oldKey.Replace('/', Path.DirectorySeparatorChar));
			Assert.True(File.Exists(oldPath));

			// invalid file rejected before any persistence: original category row untouched
			var bad = CreateFormFile([1, 2, 3], "bad.jpg", "image/jpeg");
			var result = await controller.Upsert(category, bad);
			Assert.IsType<ViewResult>(result);
			Assert.Equal(oldKey, category.ObjectKey);
			Assert.True(File.Exists(oldPath));
			// only the first upload saved (its two saves); the rejected replace saved nothing
			uow.Mock.Verify(x => x.Save(), Times.Exactly(2));
			uow.CategoryMock.Verify(x => x.Update(It.IsAny<Category>()), Times.Exactly(1));
		}
		finally { Directory.Delete(webRootPath, recursive: true); }
	}

	[Fact]
	public async Task Upsert_NewCategory_WithImage_AssignsIdUploadsAndSaves()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-category-ctrl-tests-").FullName;
		try
		{
			var existing = new Category { Id = 7, Name = "Existing", AvgShippingCost = 1, IsDeleted = false, PackagingByCategory = new() };
			var uow = new TestUnitOfWork(existing);
			var controller = CreateController(uow, webRootPath);

			var newCat = new Category { Id = 0, Name = "Fresh", AvgShippingCost = 30, IsDeleted = false };
			var png = CreateValidPngBytes();
			var result = await controller.Upsert(newCat, CreateFormFile(png, "new.png", "image/png"));

			Assert.IsType<RedirectToActionResult>(result);
			Assert.NotEqual(0, newCat.Id);
			Assert.StartsWith($"images/categories/category-{newCat.Id}/", newCat.ObjectKey);

			var savedPath = Path.Combine(webRootPath, newCat.ObjectKey!.Replace('/', Path.DirectorySeparatorChar));
			Assert.True(File.Exists(savedPath));
			uow.CategoryMock.Verify(x => x.Add(newCat), Times.Once);
			uow.Mock.Verify(x => x.Save(), Times.Exactly(2));
		}
		finally { Directory.Delete(webRootPath, recursive: true); }
	}

	[Fact]
	public async Task Upsert_NewCategory_WithInvalidImage_DoesNotPersist()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-category-ctrl-tests-").FullName;
		try
		{
			var uow = new TestUnitOfWork(new Category { Id = 7, Name = "Existing", AvgShippingCost = 1, IsDeleted = false, PackagingByCategory = new() });
			var controller = CreateController(uow, webRootPath);

			var newCat = new Category { Id = 0, Name = "Fresh", AvgShippingCost = 30, IsDeleted = false };
			var bad = CreateFormFile([1, 2, 3], "bad.jpg", "image/jpeg");
			var result = await controller.Upsert(newCat, bad);

			Assert.IsType<ViewResult>(result);
			Assert.False(controller.ModelState.IsValid);
			Assert.Equal(0, newCat.Id);
			Assert.Null(newCat.ObjectKey);
			uow.CategoryMock.Verify(x => x.Add(It.IsAny<Category>()), Times.Never);
			uow.Mock.Verify(x => x.Save(), Times.Never);
		}
		finally { Directory.Delete(webRootPath, recursive: true); }
	}

	[Fact]
	public async Task Upsert_EditWithoutImage_PreservesImageColumns()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-category-ctrl-tests-").FullName;
		try
		{
			var stored = new Category
			{
				Id = 12,
				Name = "Stored",
				AvgShippingCost = 3,
				IsDeleted = false,
				PackagingByCategory = new(),
				ImageUrl = "\\images\\categories\\category-12\\a.jpg",
				ObjectKey = "images/categories/category-12/a.jpg",
				FileName = "a.jpg",
				ContentType = "image/jpeg",
				SizeBytes = 456,
				StorageProvider = LocalImageStorageService.ProviderName,
			};
			var uow = new TestUnitOfWork(stored);
			var controller = CreateController(uow, webRootPath);

			// form posts name + cost only; image columns are not posted back
			var posted = new Category { Id = 12, Name = "Renamed", AvgShippingCost = 3, IsDeleted = false };
			var result = await controller.Upsert(posted, null);

			Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal("Renamed", stored.Name);
			Assert.Equal("images/categories/category-12/a.jpg", stored.ObjectKey);
			Assert.Equal("\\images\\categories\\category-12\\a.jpg", stored.ImageUrl);
			Assert.Equal("a.jpg", stored.FileName);
			Assert.Equal("image/jpeg", stored.ContentType);
			Assert.Equal(456, stored.SizeBytes);
			Assert.Equal(LocalImageStorageService.ProviderName, stored.StorageProvider);
			uow.Mock.Verify(x => x.Save(), Times.Exactly(1));
		}
		finally { Directory.Delete(webRootPath, recursive: true); }
	}

	[Fact]
	public async Task Upsert_ReplaceImage_WhenStorageSaveFails_KeepsOldImageAndShowsWarning()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-category-ctrl-tests-").FullName;
		try
		{
			var oldKey = "images/categories/category-10/a.jpg";
			var category = new Category
			{
				Id = 10,
				Name = "Watches",
				AvgShippingCost = 4,
				IsDeleted = false,
				PackagingByCategory = new(),
				ImageUrl = "\\images\\categories\\category-10\\a.jpg",
				ObjectKey = oldKey,
				FileName = "a.jpg",
				ContentType = "image/jpeg",
				SizeBytes = 123,
				StorageProvider = LocalImageStorageService.ProviderName,
			};
			var uow = new TestUnitOfWork(category);
			var storageMock = new Mock<IImageStorageService>();

			var svcMock = new Mock<ICategoryImageService>();
			svcMock.Setup(x => x.Validate(It.IsAny<int>(), It.IsAny<IFormFile>()));
			svcMock.Setup(x => x.SaveAsync(It.IsAny<int>(), It.IsAny<IFormFile>())).ThrowsAsync(new IOException("storage down"));
			var controller = CreateControllerWithStorage(uow, storageMock.Object, svcMock.Object);

			var result = await controller.Upsert(category, CreateFormFile([1, 2, 3], "new.jpg", "image/jpeg"));

			Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal("CategorySavedImageFailed", controller.TempData["warning"]);
			Assert.Equal(oldKey, category.ObjectKey);
			Assert.Equal("\\images\\categories\\category-10\\a.jpg", category.ImageUrl);
			storageMock.Verify(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
			uow.Mock.Verify(x => x.Save(), Times.Exactly(1));
		}
		finally { Directory.Delete(webRootPath, recursive: true); }
	}

	[Fact]
	public async Task Upsert_ReplaceImage_WhenOldCleanupFails_StillSavesNew()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-category-ctrl-tests-").FullName;
		try
		{
			var category = new Category { Id = 11, Name = "Scarves", AvgShippingCost = 7, IsDeleted = false, PackagingByCategory = new() };
			var uow = new TestUnitOfWork(category);
			var env = new Mock<IWebHostEnvironment>();
			env.Setup(x => x.WebRootPath).Returns(webRootPath);
			var storage = new LocalImageStorageService(env.Object, Mock.Of<ILogger<LocalImageStorageService>>());
			var imgLocalizer = new Mock<IStringLocalizer<CategoryImageService>>();
			imgLocalizer.Setup(x => x[It.IsAny<string>()]).Returns((string k) => new LocalizedString(k, k));
			var imgService = new CategoryImageService(storage, Mock.Of<ILogger<CategoryImageService>>(), imgLocalizer.Object);
			var controller = CreateControllerWithStorage(uow, new DeleteThrowingStorage(storage), imgService);
			var png = CreateValidPngBytes();

			await controller.Upsert(category, CreateFormFile(png, "first.png", "image/png"));
			var firstKey = category.ObjectKey!;
			var firstPath = Path.Combine(webRootPath, firstKey.Replace('/', Path.DirectorySeparatorChar));
			Assert.True(File.Exists(firstPath));

			var result = await controller.Upsert(category, CreateFormFile(png, "second.png", "image/png"));

			Assert.IsType<RedirectToActionResult>(result);
			var secondKey = category.ObjectKey!;
			Assert.NotEqual(firstKey, secondKey);
			Assert.True(File.Exists(Path.Combine(webRootPath, secondKey.Replace('/', Path.DirectorySeparatorChar))));
			// old object cleanup failed, so the old file survives (best effort) but the new one is authoritative
			Assert.True(File.Exists(firstPath));
			uow.Mock.Verify(x => x.Save(), Times.Exactly(4));
		}
		finally { Directory.Delete(webRootPath, recursive: true); }
	}

	[Fact]
	public async Task DeleteImage_ClearsColumnsThenDeletesStorage()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-category-ctrl-tests-").FullName;
		try
		{
			var category = new Category { Id = 4, Name = "Belts", AvgShippingCost = 8, IsDeleted = false, PackagingByCategory = new() };
			var uow = new TestUnitOfWork(category);
			var controller = CreateController(uow, webRootPath);
			var png = CreateValidPngBytes();
			await controller.Upsert(category, CreateFormFile(png, "img.png", "image/png"));
			var key = category.ObjectKey!;
			var path = Path.Combine(webRootPath, key.Replace('/', Path.DirectorySeparatorChar));
			Assert.True(File.Exists(path));

			var result = await controller.DeleteImage(4);
			Assert.IsType<RedirectToActionResult>(result);
			Assert.Null(category.ImageUrl);
			Assert.Null(category.ObjectKey);
			Assert.Null(category.SizeBytes);
			Assert.False(File.Exists(path));
			// two saves for the upload + one for the delete
			uow.Mock.Verify(x => x.Save(), Times.Exactly(3));
		}
		finally { Directory.Delete(webRootPath, recursive: true); }
	}

	[Fact]
	public async Task Delete_SoftDelete_WithImage_RemovesStorage_BestEffort()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-category-ctrl-tests-").FullName;
		try
		{
			var category = new Category { Id = 5, Name = "Wallets", AvgShippingCost = 12, IsDeleted = false, PackagingByCategory = new() };
			var uow = new TestUnitOfWork(category);
			var controller = CreateController(uow, webRootPath);
			var png = CreateValidPngBytes();
			await controller.Upsert(category, CreateFormFile(png, "img.png", "image/png"));
			var key = category.ObjectKey!;
			var path = Path.Combine(webRootPath, key.Replace('/', Path.DirectorySeparatorChar));
			Assert.True(File.Exists(path));
			// ensure no active products
			uow.ProductMock.Setup(x => x.GetAll(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns(new List<Product>());

			var result = await controller.Delete(5);
			var json = Assert.IsType<JsonResult>(result);
			Assert.True((bool)json.Value!.GetType().GetProperty("success")!.GetValue(json.Value)!);
			Assert.True(category.IsDeleted);
			Assert.Null(category.ObjectKey);
			Assert.False(File.Exists(path));
			// two saves for the upload + one for the soft delete
			uow.Mock.Verify(x => x.Save(), Times.Exactly(3));
		}
		finally { Directory.Delete(webRootPath, recursive: true); }
	}

	[Fact]
	public async Task Delete_SoftDelete_WhenStorageDeleteFails_CategoryStillDeleted()
	{
		var category = new Category
		{
			Id = 8,
			Name = "Socks",
			AvgShippingCost = 2,
			IsDeleted = false,
			PackagingByCategory = new(),
			ImageUrl = "\\images\\categories\\category-8\\a.jpg",
			ObjectKey = "images/categories/category-8/a.jpg",
			FileName = "a.jpg",
			ContentType = "image/jpeg",
			SizeBytes = 123,
			StorageProvider = LocalImageStorageService.ProviderName,
		};
		var uow = new TestUnitOfWork(category);
		var storageMock = new Mock<IImageStorageService>();
		storageMock.Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("boom"));
		var controller = CreateControllerWithStorage(uow, storageMock.Object);

		var result = await controller.Delete(8);
		var json = Assert.IsType<JsonResult>(result);
		Assert.True((bool)json.Value!.GetType().GetProperty("success")!.GetValue(json.Value)!);
		Assert.True(category.IsDeleted);
		Assert.Null(category.ObjectKey);
	}

	[Fact]
	public async Task DeleteImage_WhenStorageDeleteFails_DoesNotFailRequest()
	{
		// Use a mock storage that throws on Delete
		var category = new Category { Id = 6, Name = "Gloves", AvgShippingCost = 9, IsDeleted = false, PackagingByCategory = new(), ImageUrl = "\\images\\categories\\category-6\\a.jpg", ObjectKey = "images/categories/category-6/a.jpg", FileName = "a.jpg", ContentType = "image/jpeg", SizeBytes = 123, StorageProvider = LocalImageStorageService.ProviderName };
		var uow = new TestUnitOfWork(category);
		var storageMock = new Mock<IImageStorageService>();
		storageMock.Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("boom"));
		var controller = CreateControllerWithStorage(uow, storageMock.Object);

		var result = await controller.DeleteImage(6);
		Assert.IsType<RedirectToActionResult>(result);
		Assert.Null(category.ImageUrl);
		// even though storage throws, DB was consistent
		uow.Mock.Verify(x => x.Save(), Times.Once);
	}

	private static CategoryController CreateController(TestUnitOfWork uow, string webRootPath)
	{
		var env = new Mock<IWebHostEnvironment>();
		env.Setup(x => x.WebRootPath).Returns(webRootPath);
		var storage = new LocalImageStorageService(env.Object, Mock.Of<ILogger<LocalImageStorageService>>());
		var imgLocalizer = new Mock<IStringLocalizer<CategoryImageService>>();
		imgLocalizer.Setup(x => x[It.IsAny<string>()]).Returns((string k) => new LocalizedString(k, k));
		var imgService = new CategoryImageService(storage, Mock.Of<ILogger<CategoryImageService>>(), imgLocalizer.Object);
		return CreateControllerWithStorage(uow, storage, imgService);
	}

	private static CategoryController CreateControllerWithStorage(TestUnitOfWork uow, IImageStorageService storage, ICategoryImageService? svc = null)
	{
		svc ??= Mock.Of<ICategoryImageService>();
		var localizer = new Mock<IStringLocalizer<CategoryController>>();
		localizer.Setup(x => x[It.IsAny<string>()]).Returns((string n) => new LocalizedString(n, n));
		localizer.Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string n, object[] a) => new LocalizedString(n, $"{n}:{string.Join(":", a)}"));
		return new CategoryController(uow.Mock.Object, localizer.Object, svc, storage, Mock.Of<ILogger<CategoryController>>())
		{
			TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
		};
	}

	private static FormFile CreateFormFile(byte[] content, string fileName, string contentType)
		=> new(new MemoryStream(content), 0, content.Length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = contentType };

	private static byte[] CreateValidPngBytes()
	{
		using var bmp = new SKBitmap(600, 400);
		bmp.Erase(SKColors.Blue);
		using var img = SKImage.FromBitmap(bmp);
		using var data = img.Encode(SKEncodedImageFormat.Png, 100);
		return data.ToArray();
	}

	private sealed class TestUnitOfWork
	{
		public Mock<IUnitOfWork> Mock { get; } = new();
		public Mock<ICategoryRepository> CategoryMock { get; } = new();
		public Mock<IProductRepository> ProductMock { get; } = new();
		public Mock<IPackagingByCategoryRepository> PackagingByCategoryMock { get; } = new();
		public Mock<IUnitPackagingByCategoryRepository> UnitPackagingByCategoryMock { get; } = new();
		public Category Current { get; }

		private readonly List<Category> _store = new();

		public TestUnitOfWork(Category category)
		{
			Current = category;
			_store.Add(category);

			Mock.Setup(x => x.Category).Returns(CategoryMock.Object);
			Mock.Setup(x => x.Product).Returns(ProductMock.Object);
			Mock.Setup(x => x.PackagingByCategory).Returns(PackagingByCategoryMock.Object);
			Mock.Setup(x => x.UnitPackagingByCategory).Returns(UnitPackagingByCategoryMock.Object);

			// Get returns a fresh detached copy of the stored row; mutations must round-trip via Update
			CategoryMock.Setup(x => x.Get(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns((Expression<Func<Category, bool>> pred, string? inc, bool track) =>
				{
					var match = _store.FirstOrDefault(pred.Compile());
					return match is null ? null : Clone(match);
				});
			CategoryMock.Setup(x => x.Add(It.IsAny<Category>()))
				.Callback((Category c) =>
				{
					if (c.Id == 0)
						c.Id = _store.Max(x => x.Id) + 1;
					_store.Add(c);
				});
			CategoryMock.Setup(x => x.Update(It.IsAny<Category>()))
				.Callback((Category c) =>
				{
					var stored = _store.FirstOrDefault(x => x.Id == c.Id);
					if (stored is not null)
						CopyInto(c, stored);
				});

			ProductMock.Setup(x => x.GetAll(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns(new List<Product>());
			PackagingByCategoryMock.Setup(x => x.Get(It.IsAny<Expression<Func<PackagingByCategory, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns((PackagingByCategory?)null);
		}

		private static Category Clone(Category c) => new()
		{
			Id = c.Id,
			Name = c.Name,
			IsDeleted = c.IsDeleted,
			AvgShippingCost = c.AvgShippingCost,
			ImageUrl = c.ImageUrl,
			ObjectKey = c.ObjectKey,
			FileName = c.FileName,
			ContentType = c.ContentType,
			SizeBytes = c.SizeBytes,
			StorageProvider = c.StorageProvider,
			PackagingByCategory = c.PackagingByCategory,
		};

		private static void CopyInto(Category src, Category dst)
		{
			dst.Name = src.Name;
			dst.IsDeleted = src.IsDeleted;
			dst.AvgShippingCost = src.AvgShippingCost;
			dst.ImageUrl = src.ImageUrl;
			dst.ObjectKey = src.ObjectKey;
			dst.FileName = src.FileName;
			dst.ContentType = src.ContentType;
			dst.SizeBytes = src.SizeBytes;
			dst.StorageProvider = src.StorageProvider;
			dst.PackagingByCategory = src.PackagingByCategory;
		}
	}

	// For simulating a storage outage on cleanup only (saves still succeed).
	private sealed class DeleteThrowingStorage : IImageStorageService
	{
		private readonly IImageStorageService _inner;
		public DeleteThrowingStorage(IImageStorageService inner) => _inner = inner;
		public Task<StoredImage> SaveObjectAsync(ImageStorageSaveRequest request, CancellationToken cancellationToken = default)
			=> _inner.SaveObjectAsync(request, cancellationToken);
		public Task DeleteObjectAsync(DeleteObjectRequest request, CancellationToken cancellationToken = default)
			=> throw new IOException("storage cleanup simulated outage");
	}
}