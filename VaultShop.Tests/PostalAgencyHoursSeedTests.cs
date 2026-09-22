using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using System.Text.Json;
using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.DbInitializer;
using VaultShop.Models;

namespace VaultShop.Web.Tests
{
	public class PostalAgencyHoursSeedTests
	{
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

		private static void Reseed(ApplicationDbContext db)
		{
			var initializer = new DbInitializer(null!, null!, db, null!, NullLogger<DbInitializer>.Instance);
			typeof(DbInitializer).GetMethod("EnsurePostalAgencies", BindingFlags.NonPublic | BindingFlags.Instance)!
				.Invoke(initializer, null);
		}

		[Fact]
		public void Reseed_SeedsHoursMatchingJsonSample()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			using (var context = new ApplicationDbContext(options))
			{
				context.Database.EnsureCreated();
				Reseed(context);
			}

			using (var context = new ApplicationDbContext(options))
			{
				Assert.Equal(3765, context.PostalAgencies.Count());
				Assert.All(context.PostalAgencies, a => Assert.False(string.IsNullOrWhiteSpace(a.Hours)));
				var sample = context.PostalAgencies.OrderBy(a => a.Code).First();
				var json = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
					"..", "..", "..", "..", "VaultShop.DataAccess", "SeedData", "sucursales.json")));
				var rows = JsonSerializer.Deserialize<List<PostalAgency>>(json,
					new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
				Assert.Equal(rows.OrderBy(r => r.Code).First().Hours, sample.Hours);
			}
		}

		[Fact]
		public void Reseed_HoursOnlyDrift_GetsBackfilled()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			using (var context = new ApplicationDbContext(options))
			{
				context.Database.EnsureCreated();
				Reseed(context);
			}

			string code;
			string expected;
			using (var context = new ApplicationDbContext(options))
			{
				var first = context.PostalAgencies.OrderBy(a => a.Code).First();
				code = first.Code;
				expected = first.Hours;
				first.Hours = PostalAgency.HoursUnknown;
				context.SaveChanges();
			}

			using (var context = new ApplicationDbContext(options))
			{
				Reseed(context);
			}

			using (var context = new ApplicationDbContext(options))
			{
				Assert.Equal(expected, context.PostalAgencies.Find(code)!.Hours);
			}
		}

		[Fact]
		public void Reseed_MassStaleDelete_AbortedByGuard()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			using (var context = new ApplicationDbContext(options))
			{
				context.Database.EnsureCreated();
				Reseed(context);
				for (var i = 0; i < 250; i++)
				{
					context.PostalAgencies.Add(new PostalAgency
					{
						Code = $"STALE-{i:000}",
						Name = "Stale",
						ProvinceCode = "M",
						Locality = "Mendoza",
					});
				}
				context.SaveChanges();
			}

			using (var context = new ApplicationDbContext(options))
			{
				Reseed(context);
			}

			using (var context = new ApplicationDbContext(options))
			{
				Assert.Equal(3765 + 250, context.PostalAgencies.Count());
			}
		}

		[Fact]
		public void Reseed_SmallStaleDelete_RemovesStaleRows()
		{
			using var connection = CreateOpenConnection();
			var options = CreateOptions(connection);
			using (var context = new ApplicationDbContext(options))
			{
				context.Database.EnsureCreated();
				Reseed(context);
				for (var i = 0; i < 3; i++)
				{
					context.PostalAgencies.Add(new PostalAgency
					{
						Code = $"STALE-{i:000}",
						Name = "Stale",
						ProvinceCode = "M",
						Locality = "Mendoza",
					});
				}
				context.SaveChanges();
			}

			using (var context = new ApplicationDbContext(options))
			{
				Reseed(context);
			}

			using (var context = new ApplicationDbContext(options))
			{
				// ponytail: 10% threshold is unreachable at 3765 rows (stale > 376 implies stale > 200), so only the capped path is exercised.
				Assert.Equal(3765, context.PostalAgencies.Count());
				Assert.DoesNotContain(context.PostalAgencies, a => a.Code.StartsWith("STALE-", StringComparison.Ordinal));
			}
		}
	}
}
