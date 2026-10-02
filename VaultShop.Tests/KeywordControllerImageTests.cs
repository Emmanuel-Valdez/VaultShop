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
using VaultShop.Web.Areas.Admin.Controllers;
using VaultShop.Web.Services.ImageStorage;
using VaultShop.Web.Services.KeywordImages;

namespace VaultShop.Web.Tests;

public class KeywordControllerImageTests
{
	[Fact]
	public async Task Upsert_ChipThenCoverThenReplaceChip_PersistsEachIndependently()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-keyword-controller-tests-").FullName;
		try
		{
			var images = new List<KeywordImage>();
			var uow = new TestUnitOfWork(images);
			var controller = CreateController(uow, webRootPath);
			var keyword = new Keyword { Id = 1, Name = "Naruto", Slug = "naruto", IsDeleted = false };
			var png = CreateValidPngBytes();

			await controller.Upsert(keyword, CreateFormFile(png, "chip.png", "image/png"), null);

			var chip = Assert.Single(images);
			Assert.Equal(KeywordImageKind.Chip, chip.Kind);
			Assert.StartsWith("images/keywords/keyword-1/", chip.ObjectKey);
			Assert.Equal("image/jpeg", chip.ContentType);
			Assert.True(chip.SizeBytes > 0);
			var firstChipKey = chip.ObjectKey;

			await controller.Upsert(keyword, null, CreateFormFile(png, "cover.png", "image/png"));

			Assert.Equal(4, images.Count);
			var cover = Assert.Single(images, i => i.Kind == KeywordImageKind.Cover);
			var coverMedium = Assert.Single(images, i => i.Kind == KeywordImageKind.CoverMedium);
			var coverSmall = Assert.Single(images, i => i.Kind == KeywordImageKind.CoverSmall);
			Assert.StartsWith("images/keywords/keyword-1/", cover.ObjectKey);
			Assert.NotEqual(cover.ObjectKey, coverMedium.ObjectKey);
			Assert.NotEqual(cover.ObjectKey, coverSmall.ObjectKey);
			Assert.NotEqual(coverMedium.ObjectKey, coverSmall.ObjectKey);
			Assert.Equal(firstChipKey, Assert.Single(images, i => i.Kind == KeywordImageKind.Chip).ObjectKey);

			await controller.Upsert(keyword, CreateFormFile(png, "chip-replaced.png", "image/png"), null);

			Assert.Equal(4, images.Count);
			var replacedChip = Assert.Single(images, i => i.Kind == KeywordImageKind.Chip);
			Assert.NotEqual(firstChipKey, replacedChip.ObjectKey);
			Assert.StartsWith("images/keywords/keyword-1/", replacedChip.ObjectKey);
			Assert.Equal(cover.ObjectKey, Assert.Single(images, i => i.Kind == KeywordImageKind.Cover).ObjectKey);
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task Upsert_CoverReplace_RemovesOldRowsAndObjects_NoOrphans()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-keyword-controller-tests-").FullName;
		try
		{
			var images = SeedChipAndThreeCovers(webRootPath, keywordId: 1);
			var chipKey = images.Single(i => i.Kind == KeywordImageKind.Chip).ObjectKey;
			var oldCoverKeys = images.Where(i => i.Kind != KeywordImageKind.Chip).Select(i => i.ObjectKey).ToList();
			var uow = new TestUnitOfWork(images);
			var controller = CreateController(uow, webRootPath);
			var keyword = new Keyword { Id = 1, Name = "Naruto", Slug = "naruto", IsDeleted = false };

			await controller.Upsert(keyword, null, CreateFormFile(CreateValidPngBytes(), "cover.png", "image/png"));

			// still exactly 4 rows: chip + one row per cover kind
			Assert.Equal(4, images.Count);
			Assert.Single(images, i => i.Kind == KeywordImageKind.Cover);
			Assert.Single(images, i => i.Kind == KeywordImageKind.CoverMedium);
			Assert.Single(images, i => i.Kind == KeywordImageKind.CoverSmall);

			var newCoverKeys = images.Where(i => i.Kind != KeywordImageKind.Chip).Select(i => i.ObjectKey).ToList();
			Assert.Equal(3, newCoverKeys.Distinct().Count());
			foreach (var oldKey in oldCoverKeys)
			{
				Assert.DoesNotContain(oldKey, newCoverKeys);
				Assert.False(File.Exists(OnDiskPath(webRootPath, oldKey)), $"old object {oldKey} should have been deleted");
			}
			foreach (var newKey in newCoverKeys)
			{
				Assert.True(File.Exists(OnDiskPath(webRootPath, newKey)), $"new object {newKey} should exist on disk");
			}

			// chip row untouched by the cover replace
			Assert.Equal(chipKey, images.Single(i => i.Kind == KeywordImageKind.Chip).ObjectKey);
			Assert.True(File.Exists(OnDiskPath(webRootPath, chipKey)));
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task DeleteImage_CoverId_RemovesAllThreeCovers_KeepsChip()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-keyword-controller-tests-").FullName;
		try
		{
			var images = SeedChipAndThreeCovers(webRootPath, keywordId: 1);
			var chip = images.Single(i => i.Kind == KeywordImageKind.Chip);
			var medium = images.Single(i => i.Kind == KeywordImageKind.CoverMedium);
			var coverKeys = images.Where(i => i.Kind != KeywordImageKind.Chip).Select(i => i.ObjectKey).ToList();
			var uow = new TestUnitOfWork(images);
			var controller = CreateController(uow, webRootPath);

			// ANY cover id (medium here) must take the whole cover set down with it
			var result = await controller.DeleteImage(medium.Id);

			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal("Upsert", redirect.ActionName);
			var remaining = Assert.Single(images);
			Assert.Equal(KeywordImageKind.Chip, remaining.Kind);
			Assert.Equal(chip.ObjectKey, remaining.ObjectKey);
			foreach (var key in coverKeys)
			{
				Assert.False(File.Exists(OnDiskPath(webRootPath, key)), $"cover object {key} should have been deleted");
			}
			Assert.True(File.Exists(OnDiskPath(webRootPath, chip.ObjectKey)));
			Assert.True(controller.TempData.ContainsKey("success"));
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task DeleteImage_LegacySingleCover_RemovesIt()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-keyword-controller-tests-").FullName;
		try
		{
			var images = new List<KeywordImage>
			{
				new() { Id = 1, KeywordId = 1, Kind = KeywordImageKind.Chip, ObjectKey = SeedObjectFile(webRootPath, 1, "legacy-chip.jpg"), FileName = "legacy-chip.jpg", ContentType = "image/jpeg", SizeBytes = 1, StorageProvider = LocalImageStorageService.ProviderName },
				new() { Id = 2, KeywordId = 1, Kind = KeywordImageKind.Cover, ObjectKey = SeedObjectFile(webRootPath, 1, "legacy-cover.jpg"), FileName = "legacy-cover.jpg", ContentType = "image/jpeg", SizeBytes = 1, StorageProvider = LocalImageStorageService.ProviderName }
			};
			var chipKey = images[0].ObjectKey;
			var coverKey = images[1].ObjectKey;
			var uow = new TestUnitOfWork(images);
			var controller = CreateController(uow, webRootPath);

			var result = await controller.DeleteImage(2);

			Assert.IsType<RedirectToActionResult>(result);
			var remaining = Assert.Single(images);
			Assert.Equal(KeywordImageKind.Chip, remaining.Kind);
			Assert.Equal(chipKey, remaining.ObjectKey);
			Assert.False(File.Exists(OnDiskPath(webRootPath, coverKey)));
			Assert.True(File.Exists(OnDiskPath(webRootPath, chipKey)));
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task DeleteImage_ChipId_RemovesOnlyChip_CoverRowsAndFilesStay()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-keyword-controller-tests-").FullName;
		try
		{
			var images = SeedChipAndThreeCovers(webRootPath, keywordId: 1);
			var chip = images.Single(i => i.Kind == KeywordImageKind.Chip);
			var coverKeys = images.Where(i => i.Kind != KeywordImageKind.Chip).Select(i => i.ObjectKey).ToList();
			var uow = new TestUnitOfWork(images);
			var controller = CreateController(uow, webRootPath);

			var result = await controller.DeleteImage(chip.Id);

			Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal(3, images.Count);
			Assert.DoesNotContain(images, i => i.Kind == KeywordImageKind.Chip);
			Assert.Equal(3, coverKeys.Count(k => images.Any(i => i.ObjectKey == k)));
			Assert.False(File.Exists(OnDiskPath(webRootPath, chip.ObjectKey)));
			foreach (var key in coverKeys)
			{
				Assert.True(File.Exists(OnDiskPath(webRootPath, key)), $"cover object {key} should survive the chip delete");
			}
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	private static List<KeywordImage> SeedChipAndThreeCovers(string webRootPath, int keywordId)
	{
		var images = new List<KeywordImage>();
		var id = 1;
		void Add(KeywordImageKind kind, string fileName) => images.Add(new KeywordImage
		{
			Id = id++,
			KeywordId = keywordId,
			Kind = kind,
			ObjectKey = SeedObjectFile(webRootPath, keywordId, fileName),
			FileName = fileName,
			ContentType = "image/jpeg",
			SizeBytes = 1,
			StorageProvider = LocalImageStorageService.ProviderName
		});

		Add(KeywordImageKind.Chip, "old-chip.jpg");
		Add(KeywordImageKind.Cover, "old-cover.jpg");
		Add(KeywordImageKind.CoverMedium, "old-cover-medium.jpg");
		Add(KeywordImageKind.CoverSmall, "old-cover-small.jpg");
		return images;
	}

	private static string SeedObjectFile(string webRootPath, int keywordId, string fileName)
	{
		var directory = Path.Combine(webRootPath, "images", "keywords", $"keyword-{keywordId}");
		Directory.CreateDirectory(directory);
		File.WriteAllBytes(Path.Combine(directory, fileName), CreateValidPngBytes());
		return $"images/keywords/keyword-{keywordId}/{fileName}";
	}

	private static string OnDiskPath(string webRootPath, string objectKey)
		=> Path.Combine(webRootPath, objectKey.Replace('/', Path.DirectorySeparatorChar));

	private static KeywordController CreateController(TestUnitOfWork uow, string webRootPath)
	{
		var localizer = new Mock<IStringLocalizer<KeywordController>>();
		localizer.Setup(x => x[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));

		var environment = new Mock<IWebHostEnvironment>();
		environment.Setup(x => x.WebRootPath).Returns(webRootPath);
		var storage = new LocalImageStorageService(environment.Object, Mock.Of<ILogger<LocalImageStorageService>>());

		var imageLocalizer = new Mock<IStringLocalizer<KeywordImageService>>();
		imageLocalizer.Setup(x => x[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));
		var imageService = new KeywordImageService(storage, Mock.Of<ILogger<KeywordImageService>>(), imageLocalizer.Object);

		return new KeywordController(uow.Mock.Object, localizer.Object, imageService, storage, Mock.Of<ILogger<KeywordController>>())
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

	private static byte[] CreateValidPngBytes()
	{
		// ponytail: cover-sized master — chip path crops anything, cover path refuses upscales.
		using var bitmap = new SKBitmap(2100, 900);
		bitmap.Erase(SKColors.Red);
		using var image = SKImage.FromBitmap(bitmap);
		using var data = image.Encode(SKEncodedImageFormat.Png, 100);
		return data.ToArray();
	}

	private sealed class TestUnitOfWork
	{
		public Mock<IUnitOfWork> Mock { get; } = new();
		public Mock<IKeywordRepository> KeywordMock { get; } = new();
		public Mock<IKeywordImageRepository> KeywordImageMock { get; } = new();

		public TestUnitOfWork(List<KeywordImage> images)
		{
			Mock.Setup(x => x.Keyword).Returns(KeywordMock.Object);
			Mock.Setup(x => x.KeywordImage).Returns(KeywordImageMock.Object);

			KeywordMock
				.Setup(x => x.Get(It.IsAny<Expression<Func<Keyword, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns((Keyword?)null);

			var nextId = images.Count > 0 ? images.Max(i => i.Id) + 1 : 1;
			KeywordImageMock
				.Setup(x => x.Get(It.IsAny<Expression<Func<KeywordImage, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns((Expression<Func<KeywordImage, bool>> predicate, string? includeProperties, bool tracked) => images.FirstOrDefault(predicate.Compile()));
			// ponytail: real GetAll filters the backing list; includeProperties/tracking are irrelevant here.
			// Without this the cover-replace / delete-all paths silently no-op on Moq's empty default.
			KeywordImageMock
				.Setup(x => x.GetAll(It.IsAny<Expression<Func<KeywordImage, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns((Expression<Func<KeywordImage, bool>>? filter, string? includeProperties, bool tracked)
					=> filter is null ? images.AsEnumerable() : images.Where(filter.Compile()));
			KeywordImageMock
				.Setup(x => x.Add(It.IsAny<KeywordImage>()))
				.Callback((KeywordImage image) => { image.Id = nextId++; images.Add(image); });
			KeywordImageMock
				.Setup(x => x.Remove(It.IsAny<KeywordImage>()))
				.Callback((KeywordImage image) => images.Remove(image));
			KeywordImageMock
				.Setup(x => x.RemoveRange(It.IsAny<IEnumerable<KeywordImage>>()))
				.Callback((IEnumerable<KeywordImage> entities) =>
				{
					foreach (var entity in entities.ToList())
					{
						images.Remove(entity);
					}
				});
		}
	}
}
