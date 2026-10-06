using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository;
using VaultShop.Models;
using VaultShop.Web.Services.ProductVariants;

namespace VaultShop.Web.Tests
{
	public class ProductVariantServiceTests
	{
		[Fact]
		public void AddValue_CreatesTypeAndValuesWithSortOrder()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var productId = SeedProduct(options);

			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);

				foreach (var (house, order) in new[] { "Gryffindor", "Slytherin", "Ravenclaw", "Hufflepuff" }.Select((h, i) => (h, i)))
				{
					var result = service.AddValue(productId, "Casa", house, order);
					Assert.True(result.Success);
				}
				Assert.True(service.AddValue(productId, "Tamaño", "15\"", 0).Success);
				Assert.True(service.AddValue(productId, "Tamaño", "17\"", 1).Success);
			}

			using var verificationContext = new ApplicationDbContext(options);
			var data = CreateService(verificationContext).GetAdminData(productId);
			Assert.NotNull(data);
			Assert.Equal(2, data.Types.Count);
			var casa = Assert.Single(data.Types, t => t.TypeName == "Casa");
			Assert.Equal(4, casa.Values.Count);
			Assert.Equal(new[] { "Gryffindor", "Slytherin", "Ravenclaw", "Hufflepuff" }, casa.Values.Select(v => v.Value));
			var size = Assert.Single(data.Types, t => t.TypeName == "Tamaño");
			Assert.Equal(new[] { "15\"", "17\"" }, size.Values.Select(v => v.Value));
		}

		[Fact]
		public void AddValue_ReusesExistingTypeNameAcrossProducts()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var firstId = SeedProduct(options, "Backpack");
			var secondId = SeedProduct(options, "Bottle");

			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				Assert.True(service.AddValue(firstId, "Color", "Black", 0).Success);
				Assert.True(service.AddValue(secondId, "color", "White", 0).Success);
			}

			using var verificationContext = new ApplicationDbContext(options);
			Assert.Single(verificationContext.VariantOptionTypes.AsNoTracking());
			Assert.Equal("White", verificationContext.VariantOptionValues.AsNoTracking().Single(v => v.ProductId == secondId).Value);
		}

		[Fact]
		public void AddValue_DuplicateValueForSameType_IsRejected()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var productId = SeedProduct(options);

			using var context = new ApplicationDbContext(options);
			var service = CreateService(context);
			Assert.True(service.AddValue(productId, "Casa", "Gryffindor", 0).Success);

			var duplicate = service.AddValue(productId, "casa", "gryffindor", 1);

			Assert.False(duplicate.Success);
			Assert.Equal("VariantValueAlreadyExists", duplicate.ErrorKey);
		}

		[Fact]
		public void GenerateCombinations_FourByTwo_YieldsEightRows()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var productId = SeedProduct(options);
			SeedCasaTamaño(options, productId);

			IProductVariantService.VariantResult result;
			using (var context = new ApplicationDbContext(options))
			{
				result = CreateService(context).GenerateCombinations(productId);
			}

			Assert.True(result.Success);
			Assert.Equal(8, result.CreatedCount);
			using var verificationContext = new ApplicationDbContext(options);
			var data = CreateService(verificationContext).GetAdminData(productId);
			Assert.NotNull(data);
			Assert.Equal(8, data.Variants.Count);
			Assert.All(data.Variants, row => Assert.True(row.Variant.IsAvailable));
			Assert.Contains(data.Variants, row => row.Label == "Casa: Gryffindor, Tamaño: 15\"");
		}

		[Fact]
		public void GenerateCombinations_IsIdempotent()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var productId = SeedProduct(options);
			SeedCasaTamaño(options, productId);

			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				Assert.Equal(8, service.GenerateCombinations(productId).CreatedCount);
				var rerun = service.GenerateCombinations(productId);
				Assert.True(rerun.Success);
				Assert.Equal(0, rerun.CreatedCount);
			}

			using var verificationContext = new ApplicationDbContext(options);
			Assert.Equal(8, verificationContext.ProductVariants.AsNoTracking().Count(v => v.ProductId == productId));
		}

		[Fact]
		public void SetAvailability_DisabledCombination_FailsValidationButKeepsRow()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var productId = SeedProduct(options);
			SeedCasaTamaño(options, productId);

			int variantId;
			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				service.GenerateCombinations(productId);
				variantId = context.ProductVariants.AsNoTracking().First(v => v.ProductId == productId).Id;
				Assert.True(service.SetAvailability(productId, variantId, false).Success);
			}

			using var verificationContext = new ApplicationDbContext(options);
			var service2 = CreateService(verificationContext);
			Assert.False(verificationContext.ProductVariants.AsNoTracking().Single(v => v.Id == variantId).IsAvailable);
			var validation = service2.ValidateVariantForProduct(productId, variantId);
			Assert.False(validation.IsValid);
			Assert.Equal("VariantUnavailable", validation.ErrorKey);
			// Existing row intact: 8 combinations still stored.
			Assert.Equal(8, verificationContext.ProductVariants.AsNoTracking().Count(v => v.ProductId == productId));
		}

		[Fact]
		public void DeleteValue_ReferencedByCombination_IsBlocked()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var productId = SeedProduct(options);
			SeedCasaTamaño(options, productId);

			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				service.GenerateCombinations(productId);
				var valueId = context.VariantOptionValues.AsNoTracking().First(v => v.ProductId == productId).Id;

				var blocked = service.DeleteValue(productId, valueId);

				Assert.False(blocked.Success);
				Assert.Equal("VariantValueReferenced", blocked.ErrorKey);
			}

			using var verificationContext = new ApplicationDbContext(options);
			Assert.Equal(6, verificationContext.VariantOptionValues.AsNoTracking().Count(v => v.ProductId == productId));
		}

		[Fact]
		public void DeleteValue_Unreferenced_Succeeds()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var productId = SeedProduct(options);

			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				Assert.True(service.AddValue(productId, "Casa", "Gryffindor", 0).Success);
				var valueId = context.VariantOptionValues.AsNoTracking().Single(v => v.ProductId == productId).Id;

				Assert.True(service.DeleteValue(productId, valueId).Success);
			}

			using var verificationContext = new ApplicationDbContext(options);
			Assert.Empty(verificationContext.VariantOptionValues.AsNoTracking().Where(v => v.ProductId == productId));
		}

		[Fact]
		public void ValidateVariantForProduct_ForeignVariant_IsRejected()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var productId = SeedProduct(options);
			var otherId = SeedProduct(options, "Other");
			SeedCasaTamaño(options, productId);
			SeedCasaTamaño(options, otherId);

			int foreignVariantId;
			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				service.GenerateCombinations(productId);
				service.GenerateCombinations(otherId);
				foreignVariantId = context.ProductVariants.AsNoTracking().First(v => v.ProductId == otherId).Id;
			}

			using var verificationContext = new ApplicationDbContext(options);
			var validation = CreateService(verificationContext).ValidateVariantForProduct(productId, foreignVariantId);
			Assert.False(validation.IsValid);
			Assert.Equal("VariantInvalid", validation.ErrorKey);
		}

		[Fact]
		public void ValidateVariantForProduct_WrongTypeCoverage_IsRejected()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var twoTypeId = SeedProduct(options, "Two-type");
			var oneTypeId = SeedProduct(options, "One-type");
			SeedCasaTamaño(options, twoTypeId);
			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				Assert.True(service.AddValue(oneTypeId, "Talle", "M", 0).Success);
				service.GenerateCombinations(twoTypeId);
				service.GenerateCombinations(oneTypeId);
			}

			int singleTypeVariantId;
			using (var context = new ApplicationDbContext(options))
			{
				singleTypeVariantId = context.ProductVariants.AsNoTracking().Single(v => v.ProductId == oneTypeId).Id;
			}

			using var verificationContext = new ApplicationDbContext(options);
			// Single-type variant posted for a two-type product: wrong coverage.
			var crossCheck = CreateService(verificationContext).ValidateVariantForProduct(twoTypeId, singleTypeVariantId);
			Assert.False(crossCheck.IsValid);
			// Missing selection when variants exist.
			var missing = CreateService(verificationContext).ValidateVariantForProduct(twoTypeId, null);
			Assert.False(missing.IsValid);
			Assert.Equal("VariantRequired", missing.ErrorKey);
		}

		[Fact]
		public void ValidateVariantForProduct_ValidSelection_Passes()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var productId = SeedProduct(options);
			SeedCasaTamaño(options, productId);

			int variantId;
			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				service.GenerateCombinations(productId);
				variantId = context.ProductVariants.AsNoTracking().First(v => v.ProductId == productId).Id;
			}

			using var verificationContext = new ApplicationDbContext(options);
			Assert.True(CreateService(verificationContext).ValidateVariantForProduct(productId, variantId).IsValid);
		}

		[Fact]
		public void ValidateVariantForProduct_VariantLessProduct_BehavesAsBefore()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var plainId = SeedProduct(options, "Plain");
			var variantId = SeedProduct(options, "WithVariants");
			SeedCasaTamaño(options, variantId);
			using (var context = new ApplicationDbContext(options))
			{
				CreateService(context).GenerateCombinations(variantId);
			}
			int someVariantId;
			using (var context = new ApplicationDbContext(options))
			{
				someVariantId = context.ProductVariants.AsNoTracking().First(v => v.ProductId == variantId).Id;
			}

			using var verificationContext = new ApplicationDbContext(options);
			var service = CreateService(verificationContext);
			Assert.True(service.ValidateVariantForProduct(plainId, null).IsValid);
			var stray = service.ValidateVariantForProduct(plainId, someVariantId);
			Assert.False(stray.IsValid);
			Assert.Equal("VariantInvalid", stray.ErrorKey);
		}

		[Fact]
		public void ValidateVariantForProduct_StalePartialCoverage_IsRejectedByCoverageBranch()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var productId = SeedProduct(options);

			int staleVariantId;
			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				// Single-type era: generate 4 Casa-only combinations.
				foreach (var (house, order) in new[] { "Gryffindor", "Slytherin", "Ravenclaw", "Hufflepuff" }.Select((h, i) => (h, i)))
				{
					Assert.True(service.AddValue(productId, "Casa", house, order).Success);
				}
				Assert.Equal(4, service.GenerateCombinations(productId).CreatedCount);
				staleVariantId = context.ProductVariants.AsNoTracking().First(v => v.ProductId == productId).Id;
				// Second type arrives later; stale rows are NOT regenerated.
				Assert.True(service.AddValue(productId, "Tamaño", "15\"", 0).Success);
				Assert.True(service.AddValue(productId, "Tamaño", "17\"", 1).Success);
			}

			using var verificationContext = new ApplicationDbContext(options);
			var stale = verificationContext.ProductVariants.AsNoTracking().Single(v => v.Id == staleVariantId);
			// Preconditions: ownership and availability pass, so only the coverage branch can reject.
			Assert.Equal(productId, stale.ProductId);
			Assert.True(stale.IsAvailable);

			var validation = CreateService(verificationContext).ValidateVariantForProduct(productId, staleVariantId);

			Assert.False(validation.IsValid);
			Assert.Equal("VariantInvalid", validation.ErrorKey);
		}

		private static ProductVariantService CreateService(ApplicationDbContext context)
		{
			return new ProductVariantService(new UnitOfWork(context));
		}

		[Fact]
		public void GenerateCombinations_BatchesIntoSingleSave()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var productId = SeedProduct(options);
			SeedCasaTamaño(options, productId);

			int saves;
			using (var context = new CountingSaveChangesDbContext(options))
			{
				var result = CreateService(context).GenerateCombinations(productId);
				Assert.Equal(8, result.CreatedCount);
				saves = context.SaveCount;
			}

			Assert.Equal(1, saves);
			using var verificationContext = new ApplicationDbContext(options);
			Assert.Equal(8, verificationContext.ProductVariants.AsNoTracking().Count(v => v.ProductId == productId));
		}

		private sealed class CountingSaveChangesDbContext : ApplicationDbContext		{
			public int SaveCount { get; private set; }

			public CountingSaveChangesDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
			{
			}

			public override int SaveChanges()
			{
				SaveCount++;
				return base.SaveChanges();
			}
		}

		[Fact]
		public void DeleteVariant_ReferencedByCart_IsBlocked()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var productId = SeedProduct(options);
			SeedCasaTamaño(options, productId);

			int variantId;
			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				Assert.Equal(8, service.GenerateCombinations(productId).CreatedCount);
				variantId = context.ProductVariants.AsNoTracking().First(v => v.ProductId == productId).Id;
				context.ApplicationUsers.Add(new ApplicationUser { Id = "user-1", UserName = "u1@test.local", Email = "u1@test.local", Name = "U1" });
				context.ShoppingCarts.Add(new ShoppingCart { ApplicationUserId = "user-1", ProductId = productId, VariantId = variantId, Count = 1 });
				context.SaveChanges();
			}

			using (var context = new ApplicationDbContext(options))
			{
				var blocked = CreateService(context).DeleteVariant(productId, variantId);

				Assert.False(blocked.Success);
				Assert.Equal("VariantReferencedByCart", blocked.ErrorKey);
			}

			using var verificationContext = new ApplicationDbContext(options);
			Assert.NotNull(verificationContext.ProductVariants.AsNoTracking().SingleOrDefault(v => v.Id == variantId));
			Assert.NotNull(verificationContext.ShoppingCarts.AsNoTracking().SingleOrDefault(c => c.VariantId == variantId));
		}

		[Fact]
		public void DeleteVariant_Unreferenced_Succeeds()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var productId = SeedProduct(options);
			SeedCasaTamaño(options, productId);

			int variantId;
			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				service.GenerateCombinations(productId);
				variantId = context.ProductVariants.AsNoTracking().First(v => v.ProductId == productId).Id;

				Assert.True(service.DeleteVariant(productId, variantId).Success);
			}

			using var verificationContext = new ApplicationDbContext(options);
			Assert.Null(verificationContext.ProductVariants.AsNoTracking().SingleOrDefault(v => v.Id == variantId));
			Assert.Equal(7, verificationContext.ProductVariants.AsNoTracking().Count(v => v.ProductId == productId));
		}

		[Fact]
		public void Mutations_CrossProductIds_AreRejected()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			EnsureDatabaseCreated(options);
			var productId = SeedProduct(options);
			var otherId = SeedProduct(options, "Other");
			SeedCasaTamaño(options, productId);

			int valueId, variantId;
			using (var context = new ApplicationDbContext(options))
			{
				var service = CreateService(context);
				service.GenerateCombinations(productId);
				valueId = context.VariantOptionValues.AsNoTracking().First(v => v.ProductId == productId).Id;
				variantId = context.ProductVariants.AsNoTracking().First(v => v.ProductId == productId).Id;
			}

			using var verificationContext = new ApplicationDbContext(options);
			var verifyingService = CreateService(verificationContext);
			Assert.Equal("VariantInvalid", verifyingService.DeleteValue(otherId, valueId).ErrorKey);
			Assert.Equal("VariantInvalid", verifyingService.SetAvailability(otherId, variantId, false).ErrorKey);
			Assert.Equal("VariantInvalid", verifyingService.DeleteVariant(otherId, variantId).ErrorKey);
			// Target rows untouched by the rejected attempts.
			Assert.NotNull(verificationContext.VariantOptionValues.AsNoTracking().SingleOrDefault(v => v.Id == valueId));
			Assert.NotNull(verificationContext.ProductVariants.AsNoTracking().SingleOrDefault(v => v.Id == variantId));
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

		private static int SeedProduct(DbContextOptions<ApplicationDbContext> options, string name = "Test Backpack")
		{
			using var context = new ApplicationDbContext(options);
			var category = new Category { Name = $"Category for {name}", AvgShippingCost = 100m };
			var product = new Product
			{
				Name = name,
				Description = "Product used by variant tests.",
				MaxExpectation = 10,
				Category = category,
				ListPrice = 100m,
				FinalRetailPrice = 100m,
				FinalWholesalePrice = 70m,
				IsAvailableInStore = true,
				IsDeleted = false,
				StockQuantity = 5,
			};
			context.Categories.Add(category);
			context.Products.Add(product);
			context.SaveChanges();
			return product.Id;
		}

		private static void SeedCasaTamaño(DbContextOptions<ApplicationDbContext> options, int productId)
		{
			using var context = new ApplicationDbContext(options);
			var service = CreateService(context);
			foreach (var (house, order) in new[] { "Gryffindor", "Slytherin", "Ravenclaw", "Hufflepuff" }.Select((h, i) => (h, i)))
			{
				Assert.True(service.AddValue(productId, "Casa", house, order).Success);
			}
			Assert.True(service.AddValue(productId, "Tamaño", "15\"", 0).Success);
			Assert.True(service.AddValue(productId, "Tamaño", "17\"", 1).Success);
		}
	}
}
