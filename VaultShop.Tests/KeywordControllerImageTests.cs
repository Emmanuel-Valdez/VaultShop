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

			Assert.Equal(2, images.Count);
			var cover = Assert.Single(images, i => i.Kind == KeywordImageKind.Cover);
			Assert.StartsWith("images/keywords/keyword-1/", cover.ObjectKey);
			Assert.Equal(firstChipKey, Assert.Single(images, i => i.Kind == KeywordImageKind.Chip).ObjectKey);

			await controller.Upsert(keyword, CreateFormFile(png, "chip-replaced.png", "image/png"), null);

			Assert.Equal(2, images.Count);
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
		using var bitmap = new SKBitmap(600, 400);
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

			var nextId = 1;
			KeywordImageMock
				.Setup(x => x.Get(It.IsAny<Expression<Func<KeywordImage, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns((Expression<Func<KeywordImage, bool>> predicate, string? includeProperties, bool tracked) => images.FirstOrDefault(predicate.Compile()));
			KeywordImageMock
				.Setup(x => x.Add(It.IsAny<KeywordImage>()))
				.Callback((KeywordImage image) => { image.Id = nextId++; images.Add(image); });
			KeywordImageMock
				.Setup(x => x.Remove(It.IsAny<KeywordImage>()))
				.Callback((KeywordImage image) => images.Remove(image));
		}
	}
}
