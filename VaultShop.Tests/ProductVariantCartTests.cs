using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository;
using VaultShop.Models;
using VaultShop.Web.Areas.Customer.Controllers;
using VaultShop.Web.Services.Pagination;
using VaultShop.Web.Services.ProductVariants;

namespace VaultShop.Web.Tests
{
	public class ProductVariantCartTests
	{
		[Fact]
		public void DetailsPost_DifferentVariants_CreateSeparateLines()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var (productId, variantA, variantB) = SeedVariantProduct(options, stockQuantity: 5);

			// ponytail: one context per POST — mirrors production request scoping.
			using (var context = new ApplicationDbContext(options))
			{
				var controller = CreateHomeController(context);
				Assert.IsType<RedirectToActionResult>(controller.Details(new ShoppingCart { ProductId = productId, Count = 1, VariantId = variantA }));
			}
			using (var context = new ApplicationDbContext(options))
			{
				var controller = CreateHomeController(context);
				Assert.IsType<RedirectToActionResult>(controller.Details(new ShoppingCart { ProductId = productId, Count = 1, VariantId = variantB }));
			}

			using var verificationContext = new ApplicationDbContext(options);
			var lines = verificationContext.ShoppingCarts.AsNoTracking().Where(c => c.ProductId == productId).ToList();
			Assert.Equal(2, lines.Count);
			Assert.Contains(lines, c => c.VariantId == variantA && c.Count == 1);
			Assert.Contains(lines, c => c.VariantId == variantB && c.Count == 1);
		}

		[Fact]
		public void DetailsPost_SameVariant_MergesIntoExistingLine()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var (productId, variantA, _) = SeedVariantProduct(options, stockQuantity: 5);

			using (var context = new ApplicationDbContext(options))
			{
				CreateHomeController(context).Details(new ShoppingCart { ProductId = productId, Count = 1, VariantId = variantA });
			}
			using (var context = new ApplicationDbContext(options))
			{
				var controller = CreateHomeController(context);
				var result = controller.Details(new ShoppingCart { ProductId = productId, Count = 2, VariantId = variantA });

				Assert.IsType<RedirectToActionResult>(result);
				Assert.Null(controller.TempData["error"]);
			}

			using var verificationContext = new ApplicationDbContext(options);
			var line = Assert.Single(verificationContext.ShoppingCarts.AsNoTracking().Where(c => c.ProductId == productId));
			Assert.Equal(variantA, line.VariantId);
			Assert.Equal(3, line.Count);
		}

		[Fact]
		public void DetailsPost_VariantLessProduct_PreservesSingleLineMerge()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var productId = SeedPlainProduct(options, stockQuantity: 5);

			using (var context = new ApplicationDbContext(options))
			{
				CreateHomeController(context).Details(new ShoppingCart { ProductId = productId, Count = 1 });
			}
			using (var context = new ApplicationDbContext(options))
			{
				CreateHomeController(context).Details(new ShoppingCart { ProductId = productId, Count = 1 });
			}

			using var verificationContext = new ApplicationDbContext(options);
			var line = Assert.Single(verificationContext.ShoppingCarts.AsNoTracking().Where(c => c.ProductId == productId));
			Assert.Null(line.VariantId);
			Assert.Equal(2, line.Count);
		}

		[Fact]
		public void DetailsPost_SiblingLinesSumAgainstSharedStock()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var (productId, variantA, variantB) = SeedVariantProduct(options, stockQuantity: 5);

			using (var context = new ApplicationDbContext(options))
			{
				var controller = CreateHomeController(context);
				controller.Details(new ShoppingCart { ProductId = productId, Count = 2, VariantId = variantA });
			}
			using (var context = new ApplicationDbContext(options))
			{
				CreateHomeController(context).Details(new ShoppingCart { ProductId = productId, Count = 2, VariantId = variantB });
			}
			using (var context = new ApplicationDbContext(options))
			{
				var controller = CreateHomeController(context);
				// 2 + 2 existing, +2 more of any variant = 6 > 5 → rejected.
				var rejected = controller.Details(new ShoppingCart { ProductId = productId, Count = 2, VariantId = variantA });

				Assert.IsType<RedirectToActionResult>(rejected);
				Assert.Equal("NotEnoughStock", controller.TempData["error"]);
			}

			using var verificationContext = new ApplicationDbContext(options);
			var lines = verificationContext.ShoppingCarts.AsNoTracking().Where(c => c.ProductId == productId).OrderBy(c => c.VariantId).ToList();
			Assert.Equal(2, lines.Count);
			Assert.All(lines, c => Assert.Equal(2, c.Count));
		}

		[Fact]
		public void Plus_SiblingLinesExceedingStock_Rejected()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var (productId, variantA, variantB) = SeedVariantProduct(options, stockQuantity: 5);
			SeedCartLine(options, productId, variantA, count: 2);
			var lineB = SeedCartLine(options, productId, variantB, count: 3);

			using (var context = new ApplicationDbContext(options))
			{
				var controller = CreateCartController(context);
				var result = controller.Plus(lineB);

				Assert.IsType<RedirectToActionResult>(result);
				Assert.Equal("NotEnoughStock", controller.TempData["error"]);
			}

			using var verificationContext = new ApplicationDbContext(options);
			var lines = verificationContext.ShoppingCarts.AsNoTracking().Where(c => c.ProductId == productId).ToList();
			Assert.Equal(2, lines.Count);
			Assert.Contains(lines, c => c.VariantId == variantA && c.Count == 2);
			Assert.Contains(lines, c => c.VariantId == variantB && c.Count == 3);
		}

		[Fact]
		public void Plus_WithinStock_Succeeds()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var (productId, variantA, variantB) = SeedVariantProduct(options, stockQuantity: 5);
			SeedCartLine(options, productId, variantA, count: 1);
			var lineB = SeedCartLine(options, productId, variantB, count: 1);

			using (var context = new ApplicationDbContext(options))
			{
				var controller = CreateCartController(context);
				var result = controller.Plus(lineB);

				Assert.IsType<RedirectToActionResult>(result);
				Assert.Null(controller.TempData["error"]);
			}

			using var verificationContext = new ApplicationDbContext(options);
			var lines = verificationContext.ShoppingCarts.AsNoTracking().Where(c => c.ProductId == productId).ToList();
			Assert.Contains(lines, c => c.VariantId == variantA && c.Count == 1);
			Assert.Contains(lines, c => c.VariantId == variantB && c.Count == 2);
		}

		[Fact]
		public void Minus_DecrementsLine_SiblingUntouched()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var (productId, variantA, variantB) = SeedVariantProduct(options, stockQuantity: 5);
			var lineA = SeedCartLine(options, productId, variantA, count: 3);
			SeedCartLine(options, productId, variantB, count: 2);

			using (var context = new ApplicationDbContext(options))
			{
				var controller = CreateCartController(context);
				var result = controller.Minus(lineA);

				Assert.IsType<RedirectToActionResult>(result);
				Assert.Null(controller.TempData["error"]);
			}

			using var verificationContext = new ApplicationDbContext(options);
			var lines = verificationContext.ShoppingCarts.AsNoTracking().Where(c => c.ProductId == productId).ToList();
			Assert.Contains(lines, c => c.VariantId == variantA && c.Count == 2);
			Assert.Contains(lines, c => c.VariantId == variantB && c.Count == 2);
		}

		[Fact]
		public void Minus_AtOne_RemovesLine_SiblingUntouched()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var (productId, variantA, variantB) = SeedVariantProduct(options, stockQuantity: 5);
			var lineA = SeedCartLine(options, productId, variantA, count: 1);
			SeedCartLine(options, productId, variantB, count: 2);

			using (var context = new ApplicationDbContext(options))
			{
				var result = CreateCartController(context).Minus(lineA);

				Assert.IsType<RedirectToActionResult>(result);
			}

			using var verificationContext = new ApplicationDbContext(options);
			var remaining = Assert.Single(verificationContext.ShoppingCarts.AsNoTracking().Where(c => c.ProductId == productId));
			Assert.Equal(variantB, remaining.VariantId);
			Assert.Equal(2, remaining.Count);
		}

		[Fact]
		public void Remove_DeletesLine_SiblingUntouched()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var (productId, variantA, variantB) = SeedVariantProduct(options, stockQuantity: 5);
			var lineA = SeedCartLine(options, productId, variantA, count: 2);
			SeedCartLine(options, productId, variantB, count: 2);

			using (var context = new ApplicationDbContext(options))
			{
				var result = CreateCartController(context).Remove(lineA);

				Assert.IsType<RedirectToActionResult>(result);
			}

			using var verificationContext = new ApplicationDbContext(options);
			var remaining = Assert.Single(verificationContext.ShoppingCarts.AsNoTracking().Where(c => c.ProductId == productId));
			Assert.Equal(variantB, remaining.VariantId);
		}

		private static HomeController CreateHomeController(ApplicationDbContext context)
		{
			var unitOfWork = new UnitOfWork(context);
			var localizerMock = new Mock<IStringLocalizer<HomeController>>();
			localizerMock.Setup(x => x[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));

			var httpContext = new DefaultHttpContext
			{
				User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "TestAuth"))
			};
			httpContext.Session = Mock.Of<ISession>();

			return new HomeController(
				NullLogger<HomeController>.Instance,
				unitOfWork,
				localizerMock.Object,
				Options.Create(new PaginationOptions()),
				new ProductVariantService(unitOfWork))
			{
				ControllerContext = new ControllerContext { HttpContext = httpContext },
				TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
			};
		}

		private static CartController CreateCartController(ApplicationDbContext context)
		{
			var unitOfWork = new UnitOfWork(context);
			var localizerMock = new Mock<IStringLocalizer<CartController>>();
			localizerMock.Setup(x => x[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));

			var httpContext = new DefaultHttpContext
			{
				User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "TestAuth"))
			};
			httpContext.Session = Mock.Of<ISession>();

			return new CartController(
				unitOfWork,
				localizerMock.Object,
				null!,
				NullLogger<CartController>.Instance,
				null!,
				null!,
				null!,
				null!,
				null!,
				null!,
				null!)
			{
				ControllerContext = new ControllerContext { HttpContext = httpContext },
				TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
			};
		}

		private static (int productId, int variantA, int variantB) SeedVariantProduct(DbContextOptions<ApplicationDbContext> options, int stockQuantity)
		{
			using var context = new ApplicationDbContext(options);
			var category = new Category { Name = "Bags", AvgShippingCost = 100m };
			var product = new Product
			{
				Name = "Backpack",
				Description = "Variant cart test product.",
				MaxExpectation = 10,
				Category = category,
				ListPrice = 100m,
				FinalRetailPrice = 100m,
				FinalWholesalePrice = 70m,
				IsAvailableInStore = true,
				IsDeleted = false,
				StockQuantity = stockQuantity,
			};
			context.Categories.Add(category);
			context.Products.Add(product);
			context.ApplicationUsers.Add(new ApplicationUser { Id = "user-1", UserName = "u1@test.local", Email = "u1@test.local", Name = "U1" });
			context.SaveChanges();

			var service = new ProductVariantService(new UnitOfWork(context));
			foreach (var (house, order) in new[] { "Gryffindor", "Slytherin", "Ravenclaw", "Hufflepuff" }.Select((h, i) => (h, i)))
			{
				Assert.True(service.AddValue(product.Id, "Casa", house, order).Success);
			}
			Assert.True(service.AddValue(product.Id, "Tamaño", "15\"", 0).Success);
			Assert.True(service.AddValue(product.Id, "Tamaño", "17\"", 1).Success);
			Assert.Equal(8, service.GenerateCombinations(product.Id).CreatedCount);

			var ids = context.ProductVariants.AsNoTracking()
				.Where(v => v.ProductId == product.Id).OrderBy(v => v.Id).Select(v => v.Id).Take(2).ToList();
			return (product.Id, ids[0], ids[1]);
		}

		private static int SeedPlainProduct(DbContextOptions<ApplicationDbContext> options, int stockQuantity)
		{
			using var context = new ApplicationDbContext(options);
			var category = new Category { Name = "Bags", AvgShippingCost = 100m };
			var product = new Product
			{
				Name = "Plain Backpack",
				Description = "Variant-less cart test product.",
				MaxExpectation = 10,
				Category = category,
				ListPrice = 100m,
				FinalRetailPrice = 100m,
				FinalWholesalePrice = 70m,
				IsAvailableInStore = true,
				IsDeleted = false,
				StockQuantity = stockQuantity,
			};
			context.Categories.Add(category);
			context.Products.Add(product);
			context.ApplicationUsers.Add(new ApplicationUser { Id = "user-1", UserName = "u1@test.local", Email = "u1@test.local", Name = "U1" });
			context.SaveChanges();
			return product.Id;
		}

		private static int SeedCartLine(DbContextOptions<ApplicationDbContext> options, int productId, int variantId, int count)
		{
			using var context = new ApplicationDbContext(options);
			var line = new ShoppingCart { ApplicationUserId = "user-1", ProductId = productId, VariantId = variantId, Count = count };
			context.ShoppingCarts.Add(line);
			context.SaveChanges();
			return line.Id;
		}

		private static SqliteConnection CreateOpenConnection()
		{
			var connection = new SqliteConnection("Data Source=:memory:");
			connection.Open();
			return connection;
		}

		private static DbContextOptions<ApplicationDbContext> CreateOptions(SqliteConnection connection)
		{
			return new DbContextOptionsBuilder<ApplicationDbContext>()
				.UseSqlite(connection)
				.Options;
		}

		private static void EnsureDatabaseCreated(DbContextOptions<ApplicationDbContext> options)
		{
			using var context = new ApplicationDbContext(options);
			context.Database.EnsureCreated();
		}
	}
}
