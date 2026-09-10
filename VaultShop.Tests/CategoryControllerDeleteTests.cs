using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Moq;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Models.CalculatorModels;
using VaultShop.Web.Areas.Admin.Controllers;
using Xunit;

namespace VaultShop.Web.Tests
{
	public class CategoryControllerDeleteTests
	{
		[Fact]
		public void Delete_WithActiveProducts_ReturnsFalseAndDoesNotDelete()
		{
			var category = CreateCategory(id: 1);
			var uow = CreateUnitOfWork(category, activeProductCount: 2);
			var controller = CreateController(uow);

			var result = controller.Delete(1);

			var json = Assert.IsType<JsonResult>(result);
			Assert.False(GetBool(json, "success"));
			Assert.Equal("DeleteBlockedHasProducts:2", GetString(json, "message"));
			Assert.False(category.IsDeleted);
			uow.Mock.Verify(x => x.Save(), Times.Never);
		}

		[Fact]
		public void Delete_WithOnlySoftDeletedProducts_Succeeds()
		{
			var category = CreateCategory(id: 1);
			var uow = CreateUnitOfWork(category, activeProductCount: 0);
			var controller = CreateController(uow);

			var result = controller.Delete(1);

			var json = Assert.IsType<JsonResult>(result);
			Assert.True(GetBool(json, "success"));
			Assert.True(category.IsDeleted);
			uow.Mock.Verify(x => x.Save(), Times.Once);
		}

		[Fact]
		public void Delete_WithPackaging_RemovesChildrenAndParent()
		{
			var category = CreateCategory(id: 1);
			var packaging = new PackagingByCategory
			{
				Id = 10,
				CategoryId = 1,
				UnitPackagingByCategoryList = new List<UnitPackagingByCategory>
				{
					new() { Id = 20, CategoryId = 1 },
					new() { Id = 21, CategoryId = 1 }
				}
			};
			var uow = CreateUnitOfWork(category, activeProductCount: 0, packaging: packaging);
			var controller = CreateController(uow);

			var result = controller.Delete(1);

			var json = Assert.IsType<JsonResult>(result);
			Assert.True(GetBool(json, "success"));
			uow.UnitPackagingByCategoryMock.Verify(
				x => x.RemoveRange(It.Is<IEnumerable<UnitPackagingByCategory>>(list => list.Count() == 2)), Times.Once);
			uow.PackagingByCategoryMock.Verify(x => x.Remove(packaging), Times.Once);
			uow.Mock.Verify(x => x.Save(), Times.Once);
		}

		[Fact]
		public void Delete_WithoutPackaging_Succeeds()
		{
			var category = CreateCategory(id: 1);
			var uow = CreateUnitOfWork(category, activeProductCount: 0, packaging: null);
			var controller = CreateController(uow);

			var result = controller.Delete(1);

			var json = Assert.IsType<JsonResult>(result);
			Assert.True(GetBool(json, "success"));
			uow.PackagingByCategoryMock.Verify(x => x.Remove(It.IsAny<PackagingByCategory>()), Times.Never);
			uow.UnitPackagingByCategoryMock.Verify(
				x => x.RemoveRange(It.IsAny<IEnumerable<UnitPackagingByCategory>>()), Times.Never);
			uow.Mock.Verify(x => x.Save(), Times.Once);
		}

		[Fact]
		public void Delete_NullCategory_ReturnsFalse()
		{
			var uow = CreateUnitOfWork(null, activeProductCount: 0);
			var controller = CreateController(uow);

			var result = controller.Delete(999);

			var json = Assert.IsType<JsonResult>(result);
			Assert.False(GetBool(json, "success"));
			Assert.Equal("ErrorWhileDeleting", GetString(json, "message"));
			uow.Mock.Verify(x => x.Save(), Times.Never);
		}

		private static Category CreateCategory(int id) => new() { Id = id, Name = "Test", IsDeleted = false };

		private static CategoryController CreateController(TestUnitOfWork uow)
		{
			var localizer = new Mock<IStringLocalizer<CategoryController>>();
			localizer.Setup(x => x[It.IsAny<string>()])
				.Returns((string name) => new LocalizedString(name, name));
			localizer.Setup(x => x[It.IsAny<string>(), It.IsAny<object[]>()])
				.Returns((string name, object[] args) => new LocalizedString(name, $"{name}:{string.Join(":", args)}"));
			return new CategoryController(uow.Mock.Object, localizer.Object);
		}

		private static TestUnitOfWork CreateUnitOfWork(Category? category, int activeProductCount, PackagingByCategory? packaging = null)
		{
			var uow = new TestUnitOfWork();

			uow.CategoryMock
				.Setup(x => x.Get(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns(category);

			uow.ProductMock
				.Setup(x => x.GetAll(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns(Enumerable.Repeat(new Product(), activeProductCount).ToList());

			uow.PackagingByCategoryMock
				.Setup(x => x.Get(It.IsAny<Expression<Func<PackagingByCategory, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns(packaging);

			return uow;
		}

		private static bool GetBool(JsonResult json, string key)
			=> (bool)json.Value!.GetType().GetProperty(key)!.GetValue(json.Value)!;

		private static string GetString(JsonResult json, string key)
			=> (string)json.Value!.GetType().GetProperty(key)!.GetValue(json.Value)!;

		private sealed class TestUnitOfWork
		{
			public Mock<IUnitOfWork> Mock { get; } = new();
			public Mock<ICategoryRepository> CategoryMock { get; } = new();
			public Mock<IProductRepository> ProductMock { get; } = new();
			public Mock<IPackagingByCategoryRepository> PackagingByCategoryMock { get; } = new();
			public Mock<IUnitPackagingByCategoryRepository> UnitPackagingByCategoryMock { get; } = new();

			public TestUnitOfWork()
			{
				Mock.Setup(x => x.Category).Returns(CategoryMock.Object);
				Mock.Setup(x => x.Product).Returns(ProductMock.Object);
				Mock.Setup(x => x.PackagingByCategory).Returns(PackagingByCategoryMock.Object);
				Mock.Setup(x => x.UnitPackagingByCategory).Returns(UnitPackagingByCategoryMock.Object);
			}
		}
	}
}
