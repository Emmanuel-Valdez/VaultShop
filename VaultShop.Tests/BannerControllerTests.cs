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
using VaultShop.Models.ViewModels;
using VaultShop.Web.Areas.Admin.Controllers;
using VaultShop.Web.Services.BannerImages;
using VaultShop.Web.Services.ImageStorage;

namespace VaultShop.Web.Tests;

public class BannerControllerTests
{
	[Fact]
	public async Task Upsert_Create_WithCover_PersistsBannerAndImages()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-banner-controller-tests-").FullName;
		try
		{
			var uow = new TestUnitOfWork();
			var controller = CreateController(uow, webRootPath);
			var vm = new BannerVM { Banner = new Banner { Title = "Invierno", Hook = "5% off", SortOrder = 1, IsActive = true } };

			var result = await controller.Upsert(vm, CreateFormFile(CreateCoverPngBytes(), "cover.png", "image/png"), null);

			Assert.IsType<RedirectToActionResult>(result);
			var stored = Assert.Single(uow.Banners);
			Assert.Equal("Invierno", stored.Title);
			Assert.Equal(3, uow.Images.Count(i => i.BannerId == stored.Id));
			foreach (var image in uow.Images)
			{
				Assert.True(File.Exists(OnDiskPath(webRootPath, image.ObjectKey)));
			}
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task Upsert_Create_WithoutCover_ShowsErrorAndCreatesNothing()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-banner-controller-tests-").FullName;
		try
		{
			var uow = new TestUnitOfWork();
			var controller = CreateController(uow, webRootPath);
			var vm = new BannerVM { Banner = new Banner { Title = "Sin imagen" } };

			var result = await controller.Upsert(vm, null, null);

			Assert.IsType<ViewResult>(result);
			Assert.False(controller.ModelState.IsValid);
			Assert.Empty(uow.Banners);
			Assert.Empty(uow.Images);
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task Upsert_Edit_WithoutImage_PreservesImages()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-banner-controller-tests-").FullName;
		try
		{
			var uow = new TestUnitOfWork();
			var controller = CreateController(uow, webRootPath);
			var banner = new Banner { Title = "Original" };
			await controller.Upsert(new BannerVM { Banner = banner }, CreateFormFile(CreateCoverPngBytes(), "cover.png", "image/png"), null);
			var oldKeys = uow.Images.Select(i => i.ObjectKey).ToList();

			var edit = new Banner { Id = banner.Id, Title = "Editado", SortOrder = 2, IsActive = true };
			var result = await controller.Upsert(new BannerVM { Banner = edit }, null, null);

			Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal("Editado", uow.Banners.Single().Title);
			Assert.Equal(oldKeys, uow.Images.Select(i => i.ObjectKey).ToList());
			foreach (var key in oldKeys)
			{
				Assert.True(File.Exists(OnDiskPath(webRootPath, key)));
			}
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task Upsert_InvalidImage_LeavesBannerUntouched()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-banner-controller-tests-").FullName;
		try
		{
			var uow = new TestUnitOfWork();
			var controller = CreateController(uow, webRootPath);
			var banner = new Banner { Title = "Original" };
			await controller.Upsert(new BannerVM { Banner = banner }, CreateFormFile(CreateCoverPngBytes(), "cover.png", "image/png"), null);
			var oldKeys = uow.Images.Select(i => i.ObjectKey).ToList();

			var edit = new Banner { Id = banner.Id, Title = "Editado" };
			var result = await controller.Upsert(new BannerVM { Banner = edit }, CreateFormFile([1, 2, 3], "bad.jpg", "image/jpeg"), null);

			Assert.IsType<ViewResult>(result);
			Assert.Equal("Original", uow.Banners.Single().Title);
			Assert.Equal(oldKeys, uow.Images.Select(i => i.ObjectKey).ToList());
			foreach (var key in oldKeys)
			{
				Assert.True(File.Exists(OnDiskPath(webRootPath, key)));
			}
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task Delete_RemovesBannerAndCleansStorage()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-banner-controller-tests-").FullName;
		try
		{
			var uow = new TestUnitOfWork();
			var controller = CreateController(uow, webRootPath);
			var banner = new Banner { Title = "Borrar" };
			await controller.Upsert(new BannerVM { Banner = banner }, CreateFormFile(CreateCoverPngBytes(), "cover.png", "image/png"), null);
			var keys = uow.Images.Select(i => i.ObjectKey).ToList();
			Assert.NotEmpty(keys);

			var result = await controller.Delete(banner.Id);

			var json = Assert.IsType<JsonResult>(result);
			Assert.True(GetBool(json, "success"));
			Assert.Empty(uow.Banners);
			Assert.Empty(uow.Images);
			foreach (var key in keys)
			{
				Assert.False(File.Exists(OnDiskPath(webRootPath, key)));
			}
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task DeleteImage_Cover_RemovesDensitySet()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-banner-controller-tests-").FullName;
		try
		{
			var uow = new TestUnitOfWork();
			var controller = CreateController(uow, webRootPath);
			var banner = new Banner { Title = "Portada" };
			await controller.Upsert(new BannerVM { Banner = banner }, CreateFormFile(CreateCoverPngBytes(), "cover.png", "image/png"), null);
			var masterId = uow.Images.Single(i => i.Kind == BannerImageKind.Master).Id;
			var keys = uow.Images.Select(i => i.ObjectKey).ToList();

			var result = await controller.DeleteImage(masterId);

			Assert.IsType<RedirectToActionResult>(result);
			Assert.Empty(uow.Images);
			foreach (var key in keys)
			{
				Assert.False(File.Exists(OnDiskPath(webRootPath, key)));
			}
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public void GetAll_ReturnsBanners()
	{
		var uow = new TestUnitOfWork();
		uow.Banners.Add(new Banner { Title = "Uno" });
		uow.Banners.Add(new Banner { Title = "Dos" });
		var controller = CreateController(uow, Path.GetTempPath());

		var result = controller.GetAll();

		var json = Assert.IsType<JsonResult>(result);
		var data = (System.Collections.IEnumerable)json.Value!.GetType().GetProperty("data")!.GetValue(json.Value)!;
		Assert.Equal(2, data.Cast<object>().Count());
	}

	private static bool GetBool(JsonResult json, string key)
		=> (bool)json.Value!.GetType().GetProperty(key)!.GetValue(json.Value)!;

	private static string OnDiskPath(string webRootPath, string objectKey)
		=> Path.Combine(webRootPath, objectKey.Replace('/', Path.DirectorySeparatorChar));

	private static BannerController CreateController(TestUnitOfWork uow, string webRootPath)
	{
		var localizer = new Mock<IStringLocalizer<BannerController>>();
		localizer.Setup(x => x[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));

		var environment = new Mock<IWebHostEnvironment>();
		environment.Setup(x => x.WebRootPath).Returns(webRootPath);
		var storage = new LocalImageStorageService(environment.Object, Mock.Of<ILogger<LocalImageStorageService>>());

		var imageLocalizer = new Mock<IStringLocalizer<BannerImageService>>();
		imageLocalizer.Setup(x => x[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));
		var imageService = new BannerImageService(uow.Mock.Object, storage, Mock.Of<ILogger<BannerImageService>>(), imageLocalizer.Object);

		return new BannerController(uow.Mock.Object, localizer.Object, imageService, Mock.Of<ILogger<BannerController>>())
		{
			TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
		};
	}

	private static FormFile CreateFormFile(byte[] content, string fileName, string contentType)
	{
		return new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName)
		{
			Headers = new HeaderDictionary(),
			ContentType = contentType
		};
	}

	private static byte[] CreateCoverPngBytes()
	{
		using var bitmap = new SKBitmap(2100, 900);
		bitmap.Erase(SKColors.Red);
		using var image = SKImage.FromBitmap(bitmap);
		using var data = image.Encode(SKEncodedImageFormat.Png, 100);
		return data.ToArray();
	}

	private sealed class TestUnitOfWork
	{
		public Mock<IUnitOfWork> Mock { get; } = new();
		public Mock<IBannerRepository> BannerMock { get; } = new();
		public Mock<IBannerImageRepository> BannerImageMock { get; } = new();
		public Mock<ICategoryRepository> CategoryMock { get; } = new();
		public Mock<IKeywordRepository> KeywordMock { get; } = new();
		public Mock<IProductRepository> ProductMock { get; } = new();
		public List<Banner> Banners { get; } = new();
		public List<BannerImage> Images { get; } = new();

		public TestUnitOfWork()
		{
			Mock.Setup(x => x.Banner).Returns(BannerMock.Object);
			Mock.Setup(x => x.BannerImage).Returns(BannerImageMock.Object);
			Mock.Setup(x => x.Category).Returns(CategoryMock.Object);
			Mock.Setup(x => x.Keyword).Returns(KeywordMock.Object);
			Mock.Setup(x => x.Product).Returns(ProductMock.Object);

			BannerMock
				.Setup(x => x.Get(It.IsAny<Expression<Func<Banner, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns((Expression<Func<Banner, bool>> predicate, string? includeProperties, bool tracked) =>
				{
					var match = Banners.FirstOrDefault(predicate.Compile());
					return match is null ? null : Clone(match);
				});
			BannerMock
				.Setup(x => x.GetAll(It.IsAny<Expression<Func<Banner, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns((Expression<Func<Banner, bool>>? filter, string? includeProperties, bool tracked) =>
				{
					var query = filter is null ? Banners.AsEnumerable() : Banners.Where(filter.Compile());
					foreach (var banner in query)
					{
						banner.Images = Images.Where(i => i.BannerId == banner.Id).ToList();
					}
					return query;
				});
			BannerMock
				.Setup(x => x.Add(It.IsAny<Banner>()))
				.Callback((Banner banner) =>
				{
					if (banner.Id == 0)
					{
						banner.Id = Banners.Count > 0 ? Banners.Max(b => b.Id) + 1 : 1;
					}
					Banners.Add(banner);
				});
			BannerMock
				.Setup(x => x.Update(It.IsAny<Banner>()))
				.Callback((Banner banner) =>
				{
					var stored = Banners.FirstOrDefault(b => b.Id == banner.Id);
					if (stored is not null)
					{
						CopyInto(banner, stored);
					}
				});
			BannerMock
				.Setup(x => x.Remove(It.IsAny<Banner>()))
				.Callback((Banner banner) => Banners.RemoveAll(b => b.Id == banner.Id));

			var nextImageId = 1;
			BannerImageMock
				.Setup(x => x.Get(It.IsAny<Expression<Func<BannerImage, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns((Expression<Func<BannerImage, bool>> predicate, string? includeProperties, bool tracked) => Images.FirstOrDefault(predicate.Compile()));
			BannerImageMock
				.Setup(x => x.GetAll(It.IsAny<Expression<Func<BannerImage, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns((Expression<Func<BannerImage, bool>>? filter, string? includeProperties, bool tracked)
					=> filter is null ? Images.AsEnumerable() : Images.Where(filter.Compile()));
			BannerImageMock
				.Setup(x => x.Add(It.IsAny<BannerImage>()))
				.Callback((BannerImage image) => { image.Id = nextImageId++; Images.Add(image); });
			BannerImageMock
				.Setup(x => x.Remove(It.IsAny<BannerImage>()))
				.Callback((BannerImage image) => Images.Remove(image));
			BannerImageMock
				.Setup(x => x.RemoveRange(It.IsAny<IEnumerable<BannerImage>>()))
				.Callback((IEnumerable<BannerImage> entities) =>
				{
					foreach (var entity in entities.ToList())
					{
						Images.Remove(entity);
					}
				});

			CategoryMock
				.Setup(x => x.GetAll(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns(new List<Category>());
			KeywordMock
				.Setup(x => x.GetAll(It.IsAny<Expression<Func<Keyword, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns(new List<Keyword>());
			ProductMock
				.Setup(x => x.GetAll(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns(new List<Product>());
		}

		private Banner Clone(Banner source)
		{
			var clone = new Banner();
			CopyInto(source, clone);
			clone.Images = Images.Where(i => i.BannerId == source.Id).ToList();
			return clone;
		}

		private static void CopyInto(Banner source, Banner destination)
		{
			destination.Id = source.Id;
			destination.Layout = source.Layout;
			destination.TextColor = source.TextColor;
			destination.Title = source.Title;
			destination.Hook = source.Hook;
			destination.Body = source.Body;
			destination.CallToAction = source.CallToAction;
			destination.AltText = source.AltText;
			destination.TargetKind = source.TargetKind;
			destination.TargetId = source.TargetId;
			destination.SortOrder = source.SortOrder;
			destination.IsActive = source.IsActive;
		}
	}
}
