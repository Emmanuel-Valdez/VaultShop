using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Web.Areas.Admin.Controllers;
using VaultShop.Web.Services.ImageStorage;
using VaultShop.Web.Services.KeywordImages;

namespace VaultShop.Web.Tests;

public class KeywordControllerDeleteTests
{
	[Fact]
	public async Task Delete_WithActiveProducts_ReturnsFalseAndDoesNotDelete()
	{
		var keyword = CreateKeyword(id: 1);
		var links = new List<ProductKeyword>
		{
			new() { KeywordId = 1, ProductId = 10, Product = new Product { IsDeleted = false } },
			new() { KeywordId = 1, ProductId = 11, Product = new Product { IsDeleted = false } }
		};
		var uow = CreateUnitOfWork(keyword, links, new List<KeywordImage>());
		var controller = CreateController(uow);

		var result = await controller.Delete(1);

		var json = Assert.IsType<JsonResult>(result);
		Assert.False(GetBool(json, "success"));
		Assert.Equal("DeleteBlockedHasProducts:2", GetString(json, "message"));
		Assert.False(keyword.IsDeleted);
		uow.Mock.Verify(x => x.Save(), Times.Never);
	}

	[Fact]
	public async Task Delete_WithOnlySoftDeletedProducts_SucceedsAndRemovesLinksAndImages()
	{
		var keyword = CreateKeyword(id: 1);
		var links = new List<ProductKeyword>
		{
			new() { KeywordId = 1, ProductId = 10, Product = new Product { IsDeleted = true } },
			new() { KeywordId = 1, ProductId = 11, Product = new Product { IsDeleted = true } }
		};
		var images = new List<KeywordImage>
		{
			new() { Id = 5, KeywordId = 1, Kind = KeywordImageKind.Chip, ObjectKey = "keywords/keyword-1/a.jpg", StorageProvider = "local" }
		};
		var uow = CreateUnitOfWork(keyword, links, images);
		var controller = CreateController(uow);

		var result = await controller.Delete(1);

		var json = Assert.IsType<JsonResult>(result);
		Assert.True(GetBool(json, "success"));
		Assert.True(keyword.IsDeleted);
		uow.ProductKeywordMock.Verify(x => x.RemoveRange(It.Is<IEnumerable<ProductKeyword>>(l => l.Count() == 2)), Times.Once);
		uow.KeywordImageMock.Verify(x => x.RemoveRange(It.Is<IEnumerable<KeywordImage>>(l => l.Count() == 1)), Times.Once);
		uow.Mock.Verify(x => x.Save(), Times.Once);
	}

	[Fact]
	public async Task Delete_NullKeyword_ReturnsFalse()
	{
		var uow = CreateUnitOfWork(null, new List<ProductKeyword>(), new List<KeywordImage>());
		var controller = CreateController(uow);

		var result = await controller.Delete(999);

		var json = Assert.IsType<JsonResult>(result);
		Assert.False(GetBool(json, "success"));
		Assert.Equal("ErrorWhileDeleting", GetString(json, "message"));
		uow.Mock.Verify(x => x.Save(), Times.Never);
	}

	[Fact]
	public async Task Upsert_DuplicateActiveSlug_RejectedWithLocalizedError()
	{
		var keyword = CreateKeyword(id: 1);
		var duplicate = CreateKeyword(id: 2);
		duplicate.Slug = "naruto";
		var uow = CreateUnitOfWork(keyword, new List<ProductKeyword>(), new List<KeywordImage>());
		uow.KeywordMock
			.Setup(x => x.Get(It.IsAny<Expression<Func<Keyword, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
			.Returns(duplicate);

		var controller = CreateController(uow);

		var result = await controller.Upsert(keyword, null, null);

		Assert.IsType<ViewResult>(result);
		Assert.True(controller.ModelState.ContainsKey("Slug"));
		uow.KeywordMock.Verify(x => x.Add(It.IsAny<Keyword>()), Times.Never);
		uow.Mock.Verify(x => x.Save(), Times.Never);
	}

	[Fact]
	public async Task Upsert_BlankSlug_GetsSlugifiedFromName()
	{
		var keyword = CreateKeyword(id: 0);
		keyword.Slug = string.Empty;
		keyword.Name = "My Hero Academia";
		var uow = CreateUnitOfWork(null, new List<ProductKeyword>(), new List<KeywordImage>());
		uow.KeywordMock
			.Setup(x => x.Get(It.IsAny<Expression<Func<Keyword, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
			.Returns((Keyword)null!);

		var controller = CreateController(uow);

		var result = await controller.Upsert(keyword, null, null);

		Assert.IsType<RedirectToActionResult>(result);
		Assert.Equal("my-hero-academia", keyword.Slug);
		uow.KeywordMock.Verify(x => x.Add(keyword), Times.Once);
		uow.Mock.Verify(x => x.Save(), Times.Once);
	}

	private static Keyword CreateKeyword(int id) => new() { Id = id, Name = "Naruto", Slug = "naruto", IsDeleted = false };

	private static KeywordController CreateController(TestUnitOfWork uow)
	{
		var localizer = new Mock<IStringLocalizer<KeywordController>>();
		localizer.Setup(x => x[It.IsAny<string>()])
			.Returns((string name) => new LocalizedString(name, name));
		localizer.Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()])
			.Returns((string name, object[] args) => new LocalizedString(name, $"{name}:{string.Join(":", args)}"));
		return new KeywordController(uow.Mock.Object, localizer.Object, Mock.Of<IKeywordImageService>(), Mock.Of<IImageStorageService>(), Mock.Of<ILogger<KeywordController>>())
		{
			TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
		};
	}

	private static TestUnitOfWork CreateUnitOfWork(Keyword? keyword, List<ProductKeyword> links, List<KeywordImage> images)
	{
		var uow = new TestUnitOfWork();

		uow.KeywordMock
			.Setup(x => x.Get(It.IsAny<Expression<Func<Keyword, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
			.Returns(keyword);

		uow.ProductKeywordMock
			.Setup(x => x.GetAll(It.IsAny<Expression<Func<ProductKeyword, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
			.Returns(links);

		uow.KeywordImageMock
			.Setup(x => x.GetAll(It.IsAny<Expression<Func<KeywordImage, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
			.Returns(images);

		return uow;
	}

	private static bool GetBool(JsonResult json, string key)
		=> (bool)json.Value!.GetType().GetProperty(key)!.GetValue(json.Value)!;

	private static string GetString(JsonResult json, string key)
		=> (string)json.Value!.GetType().GetProperty(key)!.GetValue(json.Value)!;

	private sealed class TestUnitOfWork
	{
		public Mock<IUnitOfWork> Mock { get; } = new();
		public Mock<IKeywordRepository> KeywordMock { get; } = new();
		public Mock<IProductKeywordRepository> ProductKeywordMock { get; } = new();
		public Mock<IKeywordImageRepository> KeywordImageMock { get; } = new();

		public TestUnitOfWork()
		{
			Mock.Setup(x => x.Keyword).Returns(KeywordMock.Object);
			Mock.Setup(x => x.ProductKeyword).Returns(ProductKeywordMock.Object);
			Mock.Setup(x => x.KeywordImage).Returns(KeywordImageMock.Object);
		}
	}
}