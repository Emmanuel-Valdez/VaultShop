using System.Globalization;
using System.Security.Claims;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository;
using VaultShop.Models;
using VaultShop.Models.ViewModels;
using VaultShop.Utility;
using VaultShop.Web.Services;
using VaultShop.Web.Services.Billing;
using VaultShop.Web.Services.Branding;
using VaultShop.Web.Services.Checkout;
using VaultShop.Web.Services.Email;
using VaultShop.Web.Services.Pricing;
using VaultShop.Web.Services.ProductVariants;

namespace VaultShop.Web.Tests
{
	public class ProductVariantCheckoutTests
	{
		[Fact]
		public void CreateOrder_MultiVariantTotalExceedsStock_FailsWithoutSideEffects()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var (productId, variantA, variantB) = SeedVariantProduct(options, stockQuantity: 5);
			// Each line alone fits (3 ≤ 5); only the summed total (6 > 5) fails.
			SeedCartLine(options, "user-1", productId, variantA, count: 3);
			SeedCartLine(options, "user-1", productId, variantB, count: 3);

			using (var context = new ApplicationDbContext(options))
			{
				var result = CreateService(context).CreateOrder("user-1", CreatePostedOrderHeader(), useWholesalePrice: false);

				Assert.True(result.InsufficientStock);
				Assert.Null(result.OrderId);
			}

			using var verificationContext = new ApplicationDbContext(options);
			Assert.Empty(verificationContext.OrderHeaders.AsNoTracking());
			Assert.Empty(verificationContext.OrderDetails.AsNoTracking());
			Assert.Equal(5, verificationContext.Products.AsNoTracking().Single(p => p.Id == productId).StockQuantity);
		}

		[Fact]
		public void CreateOrder_MultiVariantSuccess_DecrementsSharedPoolOnceAndSnapshotsLabels()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var (productId, variantA, variantB) = SeedVariantProduct(options, stockQuantity: 5);
			SeedCartLine(options, "user-1", productId, variantA, count: 2);
			SeedCartLine(options, "user-1", productId, variantB, count: 1);

			string labelA, labelB;
			using (var context = new ApplicationDbContext(options))
			{
				var uow = new UnitOfWork(context);
				var service = new CheckoutService(uow, NullLogger<CheckoutService>.Instance, new ProductVariantService(uow), new DiscountEvaluator());
				var result = service.CreateOrder("user-1", CreatePostedOrderHeader(), useWholesalePrice: false);

				Assert.False(result.InsufficientStock);
				Assert.True(result.OrderId > 0);
				labelA = new ProductVariantService(uow).BuildVariantLabel(variantA);
				labelB = new ProductVariantService(uow).BuildVariantLabel(variantB);
			}

			using var verificationContext = new ApplicationDbContext(options);
			// 2 + 1 = 3 from the shared pool of 5 → 2, via a single decrement.
			Assert.Equal(2, verificationContext.Products.AsNoTracking().Single(p => p.Id == productId).StockQuantity);
			var details = verificationContext.OrderDetails.AsNoTracking().OrderBy(d => d.Id).ToList();
			Assert.Equal(2, details.Count);
			Assert.Equal(new[] { 2, 1 }, details.Select(d => d.Count).ToArray());
			Assert.Equal(new int?[] { variantA, variantB }, details.Select(d => d.VariantId).ToArray());
			Assert.Equal(new[] { labelA, labelB }, details.Select(d => d.VariantLabel).ToArray());
			Assert.Equal("Casa: Gryffindor, Tamaño: 15\"", labelA);
		}

		[Fact]
		public void CreateOrder_DisabledVariantInCart_RejectsCheckoutAndKeepsLine()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var (productId, variantA, variantB) = SeedVariantProduct(options, stockQuantity: 5);
			SeedCartLine(options, "user-1", productId, variantA, count: 1);
			SeedCartLine(options, "user-1", productId, variantB, count: 1);

			using (var context = new ApplicationDbContext(options))
			{
				Assert.True(new ProductVariantService(new UnitOfWork(context)).SetAvailability(productId, variantA, false).Success);
			}

			using (var context = new ApplicationDbContext(options))
			{
				var result = CreateService(context).CreateOrder("user-1", CreatePostedOrderHeader(), useWholesalePrice: false);

				Assert.True(result.VariantUnavailable);
				Assert.False(result.InsufficientStock);
				Assert.Null(result.OrderId);
			}

			using var verificationContext = new ApplicationDbContext(options);
			Assert.Empty(verificationContext.OrderHeaders.AsNoTracking());
			Assert.Empty(verificationContext.OrderDetails.AsNoTracking());
			Assert.Equal(5, verificationContext.Products.AsNoTracking().Single(p => p.Id == productId).StockQuantity);
			// The line stays intact for the shopper to fix; only the checkout is refused.
			Assert.Equal(2, verificationContext.ShoppingCarts.AsNoTracking().Count(c => c.ApplicationUserId == "user-1"));
		}

		[Fact]
		public void CreateOrder_RenameValueAfterwards_KeepsFrozenLabel()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var (productId, variantA, _) = SeedVariantProduct(options, stockQuantity: 5);
			SeedCartLine(options, "user-1", productId, variantA, count: 1);

			using (var context = new ApplicationDbContext(options))
			{
				var result = CreateService(context).CreateOrder("user-1", CreatePostedOrderHeader(), useWholesalePrice: false);
				Assert.True(result.OrderId > 0);
			}

			using (var context = new ApplicationDbContext(options))
			{
				var value = context.VariantOptionValues.Single(v => v.ProductId == productId && v.Value == "Gryffindor");
				value.Value = "GryffindorX";
				context.SaveChanges();
			}

			using var verificationContext = new ApplicationDbContext(options);
			var detail = Assert.Single(verificationContext.OrderDetails.AsNoTracking());
			Assert.Equal("Casa: Gryffindor, Tamaño: 15\"", detail.VariantLabel);
			var uow = new UnitOfWork(verificationContext);
			Assert.Equal("Casa: GryffindorX, Tamaño: 15\"", new ProductVariantService(uow).BuildVariantLabel(variantA));
		}

		[Fact]
		public void OrderSummary_IncludesVariantLabels()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var (productId, variantA, _) = SeedVariantProduct(options, stockQuantity: 5);
			SeedCartLine(options, "user-1", productId, variantA, count: 1);

			int orderId;
			using (var context = new ApplicationDbContext(options))
			{
				orderId = CreateService(context).CreateOrder("user-1", CreatePostedOrderHeader(), useWholesalePrice: false).OrderId!.Value;
			}

			using var verificationContext = new ApplicationDbContext(options);
			var uow = new UnitOfWork(verificationContext);
			var summary = new OrderSummaryService(uow, new OrderAccessPolicy(uow))
				.GetSummary(orderId, new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")])));

			Assert.NotNull(summary);
			var item = Assert.Single(summary.Items);
			Assert.Equal("Casa: Gryffindor, Tamaño: 15\"", item.VariantLabel);
		}

		[Fact]
		public void OrderConfirmationEmail_IncludesVariantLabel()
		{
			var labeled = new OrderItemLine("Mochila HP", 1, "$100", "Casa: Gryffindor, Tamaño: 15\"");
			var plain = new OrderItemLine("Mochila Lisa", 2, "$200");

			var content = EmailTemplates.OrderConfirmation(
				"TestStore", "Test User", 7, [labeled, plain], "$300",
				"https://test.local", new CultureInfo("es-AR"));

			Assert.Contains("Casa: Gryffindor, Tamaño: 15\"", content.Body);
			Assert.Contains("Mochila HP", content.Body);
			Assert.DoesNotContain("Mochila Lisa<br>", content.Body);
		}

		[Fact]
		public void OrderSummaryPdf_WithVariantLabels_GeneratesPdf()
		{
			var localizerMock = new Mock<IStringLocalizer<OrderSummaryPdfGenerator>>();
			localizerMock.Setup(x => x[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));
			var generator = new OrderSummaryPdfGenerator(localizerMock.Object, Options.Create(new BrandingOptions { PublicName = "TestStore" }));
			var summary = new OrderSummaryViewModel
			{
				OrderId = 7,
				OrderDate = DateTime.UtcNow,
				CustomerName = "Test User",
				Items =
				[
					new OrderSummaryItemViewModel { ProductName = "Mochila HP", VariantLabel = "Casa: Gryffindor, Tamaño: 15\"", UnitPrice = 100m, Quantity = 2 },
				],
				OrderTotal = 200m,
			};

			var pdf = generator.Generate(summary);

			Assert.NotNull(pdf);
			Assert.Equal((byte)'%', pdf[0]);
			Assert.True(pdf.Length > 100);
			// The generator renders "Mochila HP (Casa: Gryffindor, Tamaño: 15\")" — fail if the label is dropped.
			Assert.Contains("Gryffindor", PdfTextExtractor.Extract(pdf));
		}

		// ponytail: strictly sequential — proves the second checkout sees committed stock 0,
		// NOT a concurrency test. SQLite in-memory serializes writers; a real race needs PostgreSQL.
		[Fact]
		public void CreateOrder_SequentialCheckouts_SecondSeesConsumedStock()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var (productId, variantA, variantB) = SeedVariantProduct(options, stockQuantity: 1);
			using (var context = new ApplicationDbContext(options))
			{
				context.ApplicationUsers.Add(new ApplicationUser { Id = "user-2", UserName = "u2@test.local", Email = "u2@test.local", Name = "U2" });
				context.SaveChanges();
			}
			SeedCartLine(options, "user-1", productId, variantA, count: 1);
			SeedCartLine(options, "user-2", productId, variantB, count: 1);

			using (var context = new ApplicationDbContext(options))
			{
				var first = CreateService(context).CreateOrder("user-1", CreatePostedOrderHeader(), useWholesalePrice: false);
				Assert.False(first.InsufficientStock);
				Assert.True(first.OrderId > 0);
			}

			using (var context = new ApplicationDbContext(options))
			{
				var second = CreateService(context).CreateOrder("user-2", CreatePostedOrderHeader(), useWholesalePrice: false);
				Assert.True(second.InsufficientStock);
				Assert.Null(second.OrderId);
			}

			using var verificationContext = new ApplicationDbContext(options);
			Assert.Single(verificationContext.OrderHeaders.AsNoTracking());
			Assert.Single(verificationContext.OrderDetails.AsNoTracking());
			Assert.Equal(0, verificationContext.Products.AsNoTracking().Single(p => p.Id == productId).StockQuantity);
		}

		[Fact]
		public void VariantLabel_ColumnFitsLongSystemLabels()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var (productId, variantA, _) = SeedVariantProduct(options, stockQuantity: 5);
			SeedCartLine(options, "user-1", productId, variantA, count: 1);

			using (var context = new ApplicationDbContext(options))
			{
				// Model and migration agree: overlong system labels must not blow up inside checkout.
				var maxLength = context.Model.FindEntityType(typeof(OrderDetail))!
					.FindProperty(nameof(OrderDetail.VariantLabel))!.GetMaxLength();
				Assert.Equal(2000, maxLength);

				var longLabel = new string('V', 600);
				var header = new OrderHeader { ApplicationUserId = "user-1", OrderDate = DateTime.UtcNow, OrderTotal = 100m };
				context.OrderHeaders.Add(header);
				context.SaveChanges();
				context.OrderDetails.Add(new OrderDetail { ProductId = productId, OrderHeaderId = header.Id, Price = 100m, Count = 1, VariantId = variantA, VariantLabel = longLabel });
				context.SaveChanges();
			}

			using var verificationContext = new ApplicationDbContext(options);
			Assert.Equal(600, verificationContext.OrderDetails.AsNoTracking().Single().VariantLabel!.Length);
		}

		private static CheckoutService CreateService(ApplicationDbContext context)
		{
			var unitOfWork = new UnitOfWork(context);
			return new CheckoutService(unitOfWork, NullLogger<CheckoutService>.Instance, new ProductVariantService(unitOfWork), new DiscountEvaluator());
		}

		private static (int productId, int variantA, int variantB) SeedVariantProduct(DbContextOptions<ApplicationDbContext> options, int stockQuantity)
		{
			using var context = new ApplicationDbContext(options);
			var category = new Category { Name = "Bags", AvgShippingCost = 100m };
			var product = new Product
			{
				Name = "Mochila HP",
				Description = "Variant checkout test product.",
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
			context.ApplicationUsers.Add(new ApplicationUser { Id = "user-1", UserName = "u1@test.local", Email = "u1@test.local", Name = "Test User" });
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

		private static void SeedCartLine(DbContextOptions<ApplicationDbContext> options, string userId, int productId, int variantId, int count)
		{
			using var context = new ApplicationDbContext(options);
			context.ShoppingCarts.Add(new ShoppingCart { ApplicationUserId = userId, ProductId = productId, VariantId = variantId, Count = count });
			context.SaveChanges();
		}

		private static OrderHeader CreatePostedOrderHeader()
		{
			return new OrderHeader
			{
				Name = "Test User",
				StreetAddress = "123 Test St",
				City = "Buenos Aires",
				State = "Buenos Aires",
				PostalCode = "1000",
				PhoneNumber = "555-0100",
				PaymentMethod = SD.PaymentMethodStripe,
			};
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
