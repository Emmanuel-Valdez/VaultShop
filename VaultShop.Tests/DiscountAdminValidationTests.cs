using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Localization;
using Moq;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Models.ViewModels;
using VaultShop.Web.Areas.Admin.Controllers;

namespace VaultShop.Web.Tests
{
	// ponytail: controller-level discount validation only; data annotations are covered by MVC itself.
	public class DiscountAdminValidationTests
	{
		[Fact]
		public void Coupon_PercentOver100_Rejected()
		{
			var controller = CreateCouponController();
			var result = controller.Upsert(new Coupon { Code = "BIG", DiscountType = CouponDiscountType.Percent, Value = 150 });
			Assert.False(controller.ModelState.IsValid);
			Assert.IsType<ViewResult>(result);
		}

		[Fact]
		public void Coupon_DuplicateCode_Rejected()
		{
			var uow = new Mock<IUnitOfWork>();
			var coupons = new Mock<ICouponRepository>();
			coupons.Setup(x => x.Get(It.IsAny<Expression<Func<Coupon, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns(new Coupon { Id = 7, Code = "BIEN10" });
			uow.Setup(x => x.Coupon).Returns(coupons.Object);
			var controller = CreateCouponController(uow.Object);
			var result = controller.Upsert(new Coupon { Code = "bien10", DiscountType = CouponDiscountType.Percent, Value = 10 });
			Assert.False(controller.ModelState.IsValid);
			Assert.IsType<ViewResult>(result);
		}

		[Fact]
		public void Coupon_EndBeforeStart_Rejected()
		{
			var controller = CreateCouponController();
			var result = controller.Upsert(new Coupon
			{
				Code = "X",
				DiscountType = CouponDiscountType.FixedAmount,
				Value = 100,
				ValidFromUtc = new DateTime(2026, 5, 2),
				ValidToUtc = new DateTime(2026, 5, 1)
			});
			Assert.False(controller.ModelState.IsValid);
			Assert.IsType<ViewResult>(result);
		}

		[Fact]
		public void Coupon_Valid_RedirectsToIndex()
		{
			var uow = new Mock<IUnitOfWork>();
			var coupons = new Mock<ICouponRepository>();
			coupons.Setup(x => x.Get(It.IsAny<Expression<Func<Coupon, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()))
				.Returns((Coupon?)null);
			uow.Setup(x => x.Coupon).Returns(coupons.Object);
			var controller = CreateCouponController(uow.Object);
			var result = controller.Upsert(new Coupon { Code = "ok10", DiscountType = CouponDiscountType.Percent, Value = 10 });
			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal("Index", redirect.ActionName);
			coupons.Verify(x => x.Add(It.Is<Coupon>(c => c.Code == "OK10")), Times.Once);
		}

		[Fact]
		public void Promotion_BxGyZeroBuyQty_Rejected()
		{
			var (controller, _) = CreatePromotionController();
			var result = controller.Upsert(new PromotionVM
			{
				Promotion = new Promotion { Name = "2x1", Kind = PromotionKind.BxGy, Scope = PromotionScope.Store, BuyQty = 0, GetQty = 1 }
			});
			Assert.False(controller.ModelState.IsValid);
			Assert.IsType<ViewResult>(result);
		}

		[Fact]
		public void Promotion_ProductScopeWithoutTarget_Rejected()
		{
			var (controller, _) = CreatePromotionController();
			var result = controller.Upsert(new PromotionVM
			{
				Promotion = new Promotion { Name = "Off", Kind = PromotionKind.PercentOff, Scope = PromotionScope.Product, DiscountPercent = 20 }
			});
			Assert.False(controller.ModelState.IsValid);
			Assert.IsType<ViewResult>(result);
		}

		[Fact]
		public void Promotion_ValidStorePercentOff_RedirectsToIndex()
		{
			var (controller, promos) = CreatePromotionController();
			var result = controller.Upsert(new PromotionVM
			{
				Promotion = new Promotion { Name = "Sale", Kind = PromotionKind.PercentOff, Scope = PromotionScope.Store, DiscountPercent = 20 }
			});
			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal("Index", redirect.ActionName);
			promos.Verify(x => x.Add(It.IsAny<Promotion>()), Times.Once);
		}

		[Theory]
		[InlineData("es-AR", "Llevá X", "Pagá Y", "grupo X+Y")]
		[InlineData("en-US", "Take X", "Pay Y", "group X+Y")]
		public void Upsert_BxGyLabels_UseExplicitTakePayWording(string culture, string buyLabel, string payLabel, string groupWording)
		{
			var resources = new System.Resources.ResourceManager(
				"VaultShop.Web.Resources.Areas.Admin.Views.Promotion.Upsert",
				typeof(PromotionController).Assembly);
			var cultureInfo = System.Globalization.CultureInfo.GetCultureInfo(culture);

			Assert.Equal(buyLabel, resources.GetString("BuyQty", cultureInfo));
			Assert.Equal(payLabel, resources.GetString("GetQty", cultureInfo));
			Assert.Contains(groupWording, resources.GetString("BxGyHelp", cultureInfo));
		}

		private static CouponController CreateCouponController(IUnitOfWork? uow = null)
		{
			if (uow == null)
			{
				var mock = new Mock<IUnitOfWork>();
				mock.Setup(x => x.Coupon).Returns(Mock.Of<ICouponRepository>());
				uow = mock.Object;
			}
			var localizer = new Mock<IStringLocalizer<CouponController>>();
			localizer.Setup(x => x[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));
			var controller = new CouponController(uow, localizer.Object);
			controller.TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
			return controller;
		}

		private static (PromotionController, Mock<IPromotionRepository>) CreatePromotionController()
		{
			var uow = new Mock<IUnitOfWork>();
			var promos = new Mock<IPromotionRepository>();
			uow.Setup(x => x.Promotion).Returns(promos.Object);
			uow.Setup(x => x.Product).Returns(Mock.Of<IProductRepository>(r =>
				r.GetAll(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()) == Enumerable.Empty<Product>()));
			uow.Setup(x => x.Category).Returns(Mock.Of<ICategoryRepository>(r =>
				r.GetAll(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()) == Enumerable.Empty<Category>()));
			uow.Setup(x => x.Keyword).Returns(Mock.Of<IKeywordRepository>(r =>
				r.GetAll(It.IsAny<Expression<Func<Keyword, bool>>>(), It.IsAny<string?>(), It.IsAny<bool>()) == Enumerable.Empty<Keyword>()));
			var localizer = new Mock<IStringLocalizer<PromotionController>>();
			localizer.Setup(x => x[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));
			var controller = new PromotionController(uow.Object, localizer.Object);
			controller.TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
			return (controller, promos);
		}
	}
}
