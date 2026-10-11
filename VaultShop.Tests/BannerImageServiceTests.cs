using System.Linq.Expressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using SkiaSharp;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Web.Services.BannerImages;
using VaultShop.Web.Services.ImageStorage;

namespace VaultShop.Web.Tests;

public class BannerImageServiceTests
{
	[Fact]
	public async Task SaveCoverAsync_EmptyFile_RejectsWithLocalizedError()
	{
		var service = CreateService();
		var file = CreateFormFile([], "cover.jpg", "image/jpeg");

		var ex = await Assert.ThrowsAsync<BannerImageValidationException>(() => service.SaveCoverAsync(1, file));

		Assert.Equal("UploadFileEmpty", ex.Message);
	}

	[Fact]
	public async Task SaveCoverAsync_UnsupportedExtension_RejectsWithLocalizedError()
	{
		var service = CreateService();
		var file = CreateFormFile([1, 2, 3], "cover.exe", "image/jpeg");

		var ex = await Assert.ThrowsAsync<BannerImageValidationException>(() => service.SaveCoverAsync(1, file));

		Assert.Equal("UploadFileInvalidExtension", ex.Message);
	}

	[Fact]
	public async Task SaveCoverAsync_UndecodableImage_RejectsWithLocalizedError()
	{
		var service = CreateService();
		var file = CreateFormFile([1, 2, 3], "cover.jpg", "image/jpeg");

		var ex = await Assert.ThrowsAsync<BannerImageValidationException>(() => service.SaveCoverAsync(1, file));

		Assert.Equal("UploadFileInvalidImage", ex.Message);
	}

	[Fact]
	public async Task SaveCoverAsync_SmallMaster_RejectsWithoutUpscale()
	{
		var service = CreateService();
		// ponytail: 600x400 center-crops to 600x225 — below the 768x288 floor, so no variant fits.
		var small = CreateSolidPngBytes(600, 400, SKColors.Red);

		var ex = await Assert.ThrowsAsync<BannerImageValidationException>(
			() => service.SaveCoverAsync(1, CreateFormFile(small, "cover.png", "image/png")));

		Assert.Equal("UploadCoverTooSmall", ex.Message);
	}

	[Fact]
	public async Task SaveCoverAsync_FullMaster_ProducesThreeVariantsWithDistinctKeys()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-banner-image-tests-").FullName;
		try
		{
			var service = CreateService(webRootPath);
			var png = CreateSolidPngBytes(2100, 900, SKColors.Red);

			var variants = await service.SaveCoverAsync(7, CreateFormFile(png, "cover.png", "image/png"));

			Assert.NotNull(variants.Master);
			Assert.NotNull(variants.Medium);
			Assert.StartsWith("images/banners/banner-7/", variants.Master.ObjectKey);
			Assert.StartsWith("images/banners/banner-7/", variants.Medium.ObjectKey);
			Assert.StartsWith("images/banners/banner-7/", variants.Small.ObjectKey);
			Assert.NotEqual(variants.Master.ObjectKey, variants.Medium.ObjectKey);
			Assert.NotEqual(variants.Master.ObjectKey, variants.Small.ObjectKey);
			Assert.NotEqual(variants.Medium.ObjectKey, variants.Small.ObjectKey);

			using var savedMaster = SKBitmap.Decode(Path.Combine(webRootPath, variants.Master.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
			using var savedMedium = SKBitmap.Decode(Path.Combine(webRootPath, variants.Medium.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
			using var savedSmall = SKBitmap.Decode(Path.Combine(webRootPath, variants.Small.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
			Assert.Equal(1905, savedMaster.Width);
			Assert.Equal(714, savedMaster.Height);
			Assert.Equal(1280, savedMedium.Width);
			Assert.Equal(480, savedMedium.Height);
			Assert.Equal(768, savedSmall.Width);
			Assert.Equal(288, savedSmall.Height);
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task SaveCoverAsync_MediumMaster_SkipsMasterOnly()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-banner-image-tests-").FullName;
		try
		{
			var service = CreateService(webRootPath);
			// ponytail: 1400x600 center-crops to 1400x524 — covers 1280x480 but not 1905x714.
			var master = CreateSolidPngBytes(1400, 600, SKColors.Red);

			var variants = await service.SaveCoverAsync(12, CreateFormFile(master, "cover.png", "image/png"));

			Assert.Null(variants.Master);
			Assert.NotNull(variants.Medium);
			using var savedMedium = SKBitmap.Decode(Path.Combine(webRootPath, variants.Medium.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
			Assert.Equal(1280, savedMedium.Width);
			Assert.Equal(480, savedMedium.Height);
			using var savedSmall = SKBitmap.Decode(Path.Combine(webRootPath, variants.Small.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
			Assert.Equal(768, savedSmall.Width);
			Assert.Equal(288, savedSmall.Height);
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task SaveMobileAsync_TallSource_StoresPortraitAsset()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-banner-image-tests-").FullName;
		try
		{
			var service = CreateService(webRootPath);
			var portrait = CreateSolidPngBytes(1500, 2000, SKColors.Blue);

			var stored = await service.SaveMobileAsync(3, CreateFormFile(portrait, "mobile.png", "image/png"));

			Assert.NotNull(stored);
			Assert.StartsWith("images/banners/banner-3/", stored.ObjectKey);
			using var saved = SKBitmap.Decode(Path.Combine(webRootPath, stored.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
			Assert.Equal(1200, saved.Width);
			Assert.Equal(1500, saved.Height);
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task SaveMobileAsync_PhonePhoto_UpscaledToTarget()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-banner-image-tests-").FullName;
		try
		{
			var service = CreateService(webRootPath);
			// 4:5 but below the 1200x1500 source target: accepted (relaxed 600x750 gate, <=2x upscale).
			var phone = CreateSolidPngBytes(1080, 1350, SKColors.Blue);

			var stored = await service.SaveMobileAsync(3, CreateFormFile(phone, "mobile.png", "image/png"));

			Assert.NotNull(stored);
			using var saved = SKBitmap.Decode(Path.Combine(webRootPath, stored.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
			Assert.Equal(1200, saved.Width);
			Assert.Equal(1500, saved.Height);
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task SaveMobileAsync_ShortSource_ReturnsNullWithoutError()
	{
		var service = CreateService();
		// ponytail: mobile is optional — even the relaxed minimum cannot be covered, so null, not an exception.
		var landscape = CreateSolidPngBytes(800, 400, SKColors.Red);

		var stored = await service.SaveMobileAsync(3, CreateFormFile(landscape, "mobile.png", "image/png"));

		Assert.Null(stored);
	}

	[Fact]
	public async Task SaveMobileAsync_EmptyFile_RejectsWithLocalizedError()
	{
		var service = CreateService();
		var file = CreateFormFile([], "mobile.jpg", "image/jpeg");

		var ex = await Assert.ThrowsAsync<BannerImageValidationException>(() => service.SaveMobileAsync(1, file));

		Assert.Equal("UploadFileEmpty", ex.Message);
	}

	[Fact]
	public async Task ReplaceCoverAsync_RemovesOldObjectsOnlyAfterNewSave()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-banner-image-tests-").FullName;
		try
		{
			var images = new List<BannerImage>();
			var service = CreateService(webRootPath, images);
			await service.ReplaceCoverAsync(5, CreateFormFile(CreateSolidPngBytes(2100, 900, SKColors.Red), "old.png", "image/png"));
			var oldKeys = images.Select(i => i.ObjectKey).ToList();
			Assert.Equal(3, oldKeys.Count);
			foreach (var key in oldKeys)
			{
				Assert.True(File.Exists(OnDiskPath(webRootPath, key)));
			}

			await service.ReplaceCoverAsync(5, CreateFormFile(CreateSolidPngBytes(2100, 900, SKColors.Blue), "new.png", "image/png"));

			var newKeys = images.Select(i => i.ObjectKey).ToList();
			Assert.Equal(3, newKeys.Count);
			Assert.Empty(oldKeys.Intersect(newKeys));
			foreach (var key in oldKeys)
			{
				Assert.False(File.Exists(OnDiskPath(webRootPath, key)));
			}
			foreach (var key in newKeys)
			{
				Assert.True(File.Exists(OnDiskPath(webRootPath, key)));
			}
			Assert.Equal(
				[BannerImageKind.Master, BannerImageKind.Medium, BannerImageKind.Small],
				images.Select(i => i.Kind).OrderBy(k => k));
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task ReplaceCoverAsync_ValidationFailure_KeepsOldImagesUntouched()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-banner-image-tests-").FullName;
		try
		{
			var images = new List<BannerImage>();
			var service = CreateService(webRootPath, images);
			await service.ReplaceCoverAsync(5, CreateFormFile(CreateSolidPngBytes(2100, 900, SKColors.Red), "old.png", "image/png"));
			var oldKeys = images.Select(i => i.ObjectKey).ToList();

			var ex = await Assert.ThrowsAsync<BannerImageValidationException>(
				() => service.ReplaceCoverAsync(5, CreateFormFile([1, 2, 3], "bad.jpg", "image/jpeg")));

			Assert.Equal("UploadFileInvalidImage", ex.Message);
			Assert.Equal(oldKeys, images.Select(i => i.ObjectKey).ToList());
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
	public async Task DeleteAllAsync_RemovesRowsAndLeavesNoObjects()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-banner-image-tests-").FullName;
		try
		{
			var images = new List<BannerImage>();
			var service = CreateService(webRootPath, images);
			await service.ReplaceCoverAsync(9, CreateFormFile(CreateSolidPngBytes(2100, 900, SKColors.Red), "cover.png", "image/png"));
			await service.ReplaceMobileAsync(9, CreateFormFile(CreateSolidPngBytes(1500, 2000, SKColors.Blue), "mobile.png", "image/png"));
			Assert.Equal(4, images.Count);

			await service.DeleteAllAsync(9);

			Assert.Empty(images);
			var bannerDir = Path.Combine(webRootPath, "images", "banners", "banner-9");
			Assert.True(!Directory.Exists(bannerDir) || Directory.GetFiles(bannerDir, "*", SearchOption.AllDirectories).Length == 0);
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	private static string OnDiskPath(string webRootPath, string objectKey)
		=> Path.Combine(webRootPath, objectKey.Replace('/', Path.DirectorySeparatorChar));

	private static BannerImageService CreateService(string? webRootPath = null, List<BannerImage>? images = null)
	{
		var environment = new Mock<IWebHostEnvironment>();
		environment.Setup(x => x.WebRootPath).Returns(webRootPath ?? Path.GetTempPath());
		var imageStorageService = new LocalImageStorageService(environment.Object, Mock.Of<ILogger<LocalImageStorageService>>());

		var localizer = new Mock<IStringLocalizer<BannerImageService>>();
		localizer.Setup(x => x[It.IsAny<string>()])
			.Returns((string key) => new LocalizedString(key, key));

		return new BannerImageService(CreateUnitOfWork(images ?? new List<BannerImage>()), imageStorageService, Mock.Of<ILogger<BannerImageService>>(), localizer.Object);
	}

	private static IUnitOfWork CreateUnitOfWork(List<BannerImage> images)
	{
		var uow = new Mock<IUnitOfWork>();
		var repo = new Mock<IBannerImageRepository>();
		uow.Setup(x => x.BannerImage).Returns(repo.Object);

		var nextId = images.Count > 0 ? images.Max(i => i.Id) + 1 : 1;
		repo
			.Setup(x => x.GetAll(It.IsAny<Expression<Func<BannerImage, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
			.Returns((Expression<Func<BannerImage, bool>>? filter, string? includeProperties, bool tracked)
				=> filter is null ? images.AsEnumerable() : images.Where(filter.Compile()));
		repo
			.Setup(x => x.Add(It.IsAny<BannerImage>()))
			.Callback((BannerImage image) => { image.Id = nextId++; images.Add(image); });
		repo
			.Setup(x => x.Remove(It.IsAny<BannerImage>()))
			.Callback((BannerImage image) => images.Remove(image));
		repo
			.Setup(x => x.RemoveRange(It.IsAny<IEnumerable<BannerImage>>()))
			.Callback((IEnumerable<BannerImage> entities) =>
			{
				foreach (var entity in entities.ToList())
				{
					images.Remove(entity);
				}
			});

		return uow.Object;
	}

	private static FormFile CreateFormFile(byte[] content, string fileName, string contentType)
	{
		return new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName)
		{
			Headers = new HeaderDictionary(),
			ContentType = contentType
		};
	}

	private static byte[] CreateSolidPngBytes(int width, int height, SKColor color)
	{
		using var bitmap = new SKBitmap(width, height);
		bitmap.Erase(color);
		using var image = SKImage.FromBitmap(bitmap);
		using var data = image.Encode(SKEncodedImageFormat.Png, 100);
		return data.ToArray();
	}
}
