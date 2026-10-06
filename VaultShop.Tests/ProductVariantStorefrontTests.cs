using System.Linq.Expressions;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Web.Areas.Customer.Controllers;
using VaultShop.Web.Services.Pagination;
using VaultShop.Web.Services.ProductVariants;

namespace VaultShop.Web.Tests
{
	public class ProductVariantStorefrontTests
	{
		[Theory]
		[InlineData(null, "VariantRequired")]
		[InlineData(999, "VariantInvalid")]
		public void DetailsPost_MissingOrForeignVariant_RejectedWithEmptyCart(int? variantId, string errorKey)
		{
			var (controller, unitOfWorkMock, cartMock) = CreateControllerWithVariants(
				validation: IProductVariantService.VariantValidationResult.Invalid(errorKey));

			var result = controller.Details(new ShoppingCart { ProductId = 1, Count = 1, VariantId = variantId });

			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal(nameof(HomeController.Details), redirect.ActionName);
			Assert.Equal(errorKey, controller.TempData["error"]);
			cartMock.Verify(c => c.Add(It.IsAny<ShoppingCart>()), Times.Never);
			cartMock.Verify(c => c.Update(It.IsAny<ShoppingCart>()), Times.Never);
			unitOfWorkMock.Verify(u => u.Save(), Times.Never);
		}

		[Fact]
		public void DetailsPost_DisabledVariant_RejectedWithEmptyCart()
		{
			var (controller, _, cartMock) = CreateControllerWithVariants(
				validation: IProductVariantService.VariantValidationResult.Invalid("VariantUnavailable"));

			var result = controller.Details(new ShoppingCart { ProductId = 1, Count = 1, VariantId = 7 });

			Assert.Equal("VariantUnavailable", controller.TempData["error"]);
			Assert.IsType<RedirectToActionResult>(result);
			cartMock.Verify(c => c.Add(It.IsAny<ShoppingCart>()), Times.Never);
		}

		[Fact]
		public void DetailsPost_ValidVariant_ProceedsToCartWrite()
		{
			var (controller, unitOfWorkMock, _) = CreateControllerWithVariants(
				validation: IProductVariantService.VariantValidationResult.Valid());

			var result = controller.Details(new ShoppingCart { ProductId = 1, Count = 1, VariantId = 3 });

			var redirect = Assert.IsType<RedirectToActionResult>(result);
			Assert.Equal(nameof(HomeController.Details), redirect.ActionName);
			Assert.Null(controller.TempData["error"]);
			unitOfWorkMock.Verify(u => u.Save(), Times.Once);
		}

		[Fact]
		public void DetailsGet_VariantLessProduct_SetsNoSelectionData()
		{
			var (controller, _, _) = CreateControllerWithVariants(
				validation: IProductVariantService.VariantValidationResult.Valid(),
				selectionData: null);

			var result = controller.Details(1, null);

			var view = Assert.IsType<ViewResult>(result);
			Assert.Null(view.ViewData["VariantSelection"]);
		}

		[Fact]
		public void DetailsGet_VariantProduct_ExposesSelectionData()
		{
			var selection = new IProductVariantService.VariantSelectionData();
			var (controller, _, _) = CreateControllerWithVariants(
				validation: IProductVariantService.VariantValidationResult.Valid(),
				selectionData: selection);

			var result = controller.Details(1, null);

			var view = Assert.IsType<ViewResult>(result);
			Assert.Same(selection, view.ViewData["VariantSelection"]);
		}

		// 3.1 — rendered page carries one selector per type, all values, combination data, and no preselection.
		[Fact]
		public async Task Details_TwoTypeProduct_RendersSelectorsAndCombinationData()
		{
			using var factory = new CustomWebApplicationFactory();
			var productId = SeedVariantProduct(factory);
			var client = factory.CreateClient();

			var body = System.Net.WebUtility.HtmlDecode(await client.GetStringAsync($"/en-US/Customer/Home/Details/{productId}"));

			Assert.Contains("data-variant-picker", body);
			Assert.Contains("Casa", body);
			Assert.Contains("Tamaño", body);
			foreach (var house in new[] { "Gryffindor", "Slytherin", "Ravenclaw", "Hufflepuff" })
			{
				Assert.Contains(house, body);
			}
			Assert.Contains("name=\"VariantId\"", body);
			// 8 combinations embedded for the client-side tuple → VariantId mapping.
			var combos = Regex.Match(body, @"<script type=""application/json"" id=""variant-combinations"">(.+?)</script>", RegexOptions.Singleline);
			Assert.True(combos.Success, "expected embedded combination data");
			Assert.Equal(8, Regex.Matches(combos.Groups[1].Value, @"""id""").Count);
			// No default: the placeholder stays unselected and no VariantId is preset.
			Assert.Contains("<option value=\"\">", body);
			var options = Regex.Matches(body, "<option[^>]*>");
			Assert.NotEmpty(options);
			Assert.All(options, o => Assert.DoesNotContain("selected", o.Value));
			// Picker script hooks all present in the same markup (button starts disabled, set by script on load).
			Assert.Contains("id=\"variant-combinations\"", body);
			Assert.Contains("data-variant-selector", body);
			Assert.Contains("data-variant-unavailable", body);
			Assert.Contains("product-detail__add-button", body);
		}

		// 3.3 — variant-less page renders no selectors and keeps the legacy form.
		[Fact]
		public async Task Details_VariantLessProduct_RendersNoSelectors()
		{
			using var factory = new CustomWebApplicationFactory();
			var productId = SeedPlainProduct(factory);
			var client = factory.CreateClient();

			var body = System.Net.WebUtility.HtmlDecode(await client.GetStringAsync($"/en-US/Customer/Home/Details/{productId}"));

			Assert.DoesNotContain("data-variant-picker", body);
			Assert.DoesNotContain("variant-combinations", body);
			Assert.DoesNotContain("name=\"VariantId\"", body);
			Assert.Contains("product-detail__add-button", body);
		}

		// 3.4 — variants add no URLs: one Details loc per product, nothing variant-shaped.
		[Fact]
		public async Task Sitemap_VariantProduct_EmitsSingleProductUrlAndNoVariantUrls()
		{
			using var factory = new CustomWebApplicationFactory();
			var productId = SeedVariantProduct(factory);
			var client = factory.CreateClient();

			var xml = await client.GetStringAsync("/sitemap.xml");

			Assert.Single(Regex.Matches(xml, $"Details/{productId}"));
			Assert.DoesNotContain("variant", xml, StringComparison.OrdinalIgnoreCase);
		}

		private static (HomeController Controller, Mock<IUnitOfWork> UnitOfWorkMock, Mock<IShoppingCartRepository> CartMock) CreateControllerWithVariants(
			IProductVariantService.VariantValidationResult validation,
			IProductVariantService.VariantSelectionData? selectionData = null)
		{
			var unitOfWorkMock = new Mock<IUnitOfWork>();
			var productMock = new Mock<IProductRepository>();
			var cartMock = new Mock<IShoppingCartRepository>();
			unitOfWorkMock.SetupGet(u => u.Product).Returns(productMock.Object);
			unitOfWorkMock.SetupGet(u => u.ShoppingCart).Returns(cartMock.Object);
			unitOfWorkMock.SetupGet(u => u.FavoriteProduct).Returns(Mock.Of<IFavoriteProductRepository>());
			productMock.Setup(p => p.Get(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string>(), false))
				.Returns(new Product
				{
					Id = 1,
					Name = "Backpack",
					Slug = "backpack",
					StockQuantity = 5,
					IsDeleted = false,
					IsAvailableInStore = true,
					Category = new Category { Id = 1, Name = "Bags" },
				});
			cartMock.Setup(c => c.Get(It.IsAny<Expression<Func<ShoppingCart, bool>>>(), It.IsAny<string>(), false))
				.Returns((ShoppingCart?)null);
			cartMock.Setup(c => c.GetAll(It.IsAny<Expression<Func<ShoppingCart, bool>>>(), It.IsAny<string>(), false))
				.Returns(new List<ShoppingCart>());

			var variantMock = new Mock<IProductVariantService>();
			variantMock.Setup(v => v.ValidateVariantForProduct(It.IsAny<int>(), It.IsAny<int?>())).Returns(validation);
			variantMock.Setup(v => v.GetSelectionData(It.IsAny<int>())).Returns(selectionData);

			var localizerMock = new Mock<IStringLocalizer<HomeController>>();
			localizerMock.Setup(x => x[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));

			var httpContext = new DefaultHttpContext
			{
				User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "TestAuth"))
			};
			httpContext.Session = Mock.Of<ISession>();

			var controller = new HomeController(
				NullLogger<HomeController>.Instance,
				unitOfWorkMock.Object,
				localizerMock.Object,
				Options.Create(new PaginationOptions()),
				variantMock.Object)
			{
				ControllerContext = new ControllerContext { HttpContext = httpContext },
				TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>()),
				Url = Mock.Of<IUrlHelper>(),
			};

			return (controller, unitOfWorkMock, cartMock);
		}

		// hardening 3.1 — sibling variant lines render distinguishable labels on the cart page.
		[Fact]
		public async Task CartIndex_TwoVariants_ShowsDistinguishableLabels()
		{
			using var factory = new CustomWebApplicationFactory();
			string labelA, labelB;
			using (var scope = factory.Services.CreateScope())
			{
				var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
				var category = new Category { Name = "Mochilas", Slug = "mochilas", AvgShippingCost = 100m };
				db.Categories.Add(category);
				db.SaveChanges();
				var product = new Product
				{
					Name = "Mochila HP",
					Slug = "mochila-hp",
					Description = "Mochila",
					MaxExpectation = 10,
					CategoryId = category.Id,
					ListPrice = 100m,
					FinalRetailPrice = 100m,
					FinalWholesalePrice = 100m,
					IsAvailableInStore = true,
					IsDeleted = false,
					StockQuantity = 10,
				};
				db.Products.Add(product);
				db.SaveChanges();

				var variants = scope.ServiceProvider.GetRequiredService<IProductVariantService>();
				foreach (var (house, order) in new[] { "Gryffindor", "Slytherin" }.Select((h, i) => (h, i)))
				{
					Assert.True(variants.AddValue(product.Id, "Casa", house, order).Success);
				}
				Assert.Equal(2, variants.GenerateCombinations(product.Id).CreatedCount);
				var ids = db.ProductVariants.AsNoTracking().Where(v => v.ProductId == product.Id).OrderBy(v => v.Id).Select(v => v.Id).ToList();
				labelA = variants.BuildVariantLabel(ids[0]);
				labelB = variants.BuildVariantLabel(ids[1]);
				var userId = db.ApplicationUsers.AsNoTracking().Single(u => u.UserName == factory.CustomerEmail).Id;
				db.ShoppingCarts.Add(new ShoppingCart { ApplicationUserId = userId, ProductId = product.Id, VariantId = ids[0], Count = 1 });
				db.ShoppingCarts.Add(new ShoppingCart { ApplicationUserId = userId, ProductId = product.Id, VariantId = ids[1], Count = 1 });
				db.SaveChanges();
			}

			Assert.NotEqual(labelA, labelB);
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
			await TestAuthHelper.LoginAsync(client, factory.CustomerEmail, factory.TestPassword);

			var body = System.Net.WebUtility.HtmlDecode(await client.GetStringAsync("/en-US/Customer/Cart/Index"));

			Assert.Contains(labelA, body);
			Assert.Contains(labelB, body);
		}

		private static int SeedVariantProduct(CustomWebApplicationFactory factory)
		{
			using var scope = factory.Services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var category = new Category { Name = "Mochilas", Slug = "mochilas", AvgShippingCost = 100m };
			db.Categories.Add(category);
			db.SaveChanges();
			var product = new Product
			{
				Name = "Mochila HP",
				Slug = "mochila-hp",
				Description = "Mochila",
				MaxExpectation = 10,
				CategoryId = category.Id,
				ListPrice = 100m,
				FinalRetailPrice = 100m,
				FinalWholesalePrice = 100m,
				IsAvailableInStore = true,
				IsDeleted = false,
				StockQuantity = 10,
			};
			db.Products.Add(product);
			db.SaveChanges();

			var variants = scope.ServiceProvider.GetRequiredService<IProductVariantService>();
			foreach (var (house, order) in new[] { "Gryffindor", "Slytherin", "Ravenclaw", "Hufflepuff" }.Select((h, i) => (h, i)))
			{
				Assert.True(variants.AddValue(product.Id, "Casa", house, order).Success);
			}
			Assert.True(variants.AddValue(product.Id, "Tamaño", "15\"", 0).Success);
			Assert.True(variants.AddValue(product.Id, "Tamaño", "17\"", 1).Success);
			var generated = variants.GenerateCombinations(product.Id);
			Assert.True(generated.Success);
			Assert.Equal(8, generated.CreatedCount);

			return product.Id;
		}

		private static int SeedPlainProduct(CustomWebApplicationFactory factory)
		{
			using var scope = factory.Services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var category = new Category { Name = "Mochilas", Slug = "mochilas", AvgShippingCost = 100m };
			db.Categories.Add(category);
			db.SaveChanges();
			var product = new Product
			{
				Name = "Mochila Lisa",
				Slug = "mochila-lisa",
				Description = "Mochila",
				MaxExpectation = 10,
				CategoryId = category.Id,
				ListPrice = 100m,
				FinalRetailPrice = 100m,
				FinalWholesalePrice = 100m,
				IsAvailableInStore = true,
				IsDeleted = false,
				StockQuantity = 10,
			};
			db.Products.Add(product);
			db.SaveChanges();

			return product.Id;
		}
	}
}
