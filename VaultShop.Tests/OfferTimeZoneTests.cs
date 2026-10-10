using VaultShop.Web.Services.Pricing;

namespace VaultShop.Web.Tests
{
	// oferta-huso-horario-ar 1.1: admin wall time is Argentina; storage/eval stays UTC.
	public class OfferTimeZoneTests
	{
		[Fact]
		public void Zone_Resolves_OnCurrentPlatform()
		{
			var zone = OfferTimeZone.ArgentinaZone;
			Assert.Contains(zone.Id, new[] { OfferTimeZone.IanaId, OfferTimeZone.WindowsId });
		}

		[Fact]
		public void RoundTrip_PreservesWallTime()
		{
			var wall = new DateTime(2026, 10, 7, 18, 0, 0);
			var utc = OfferTimeZone.ToUtc(wall);
			Assert.Equal(new DateTime(2026, 10, 7, 21, 0, 0, DateTimeKind.Utc), utc);
			Assert.Equal(wall, OfferTimeZone.ToLocal(utc));
		}

		[Fact]
		public void MidnightBoundary_FallsOnNextUtcDay()
		{
			var utc = OfferTimeZone.ToUtc(new DateTime(2026, 10, 8, 0, 30, 0));
			Assert.Equal(new DateTime(2026, 10, 8, 3, 30, 0, DateTimeKind.Utc), utc);
		}

		[Fact]
		public void Null_StaysNull()
		{
			Assert.Null(OfferTimeZone.ToUtc(null));
			Assert.Null(OfferTimeZone.ToLocal(null));
		}

		// 1.2 — the three admin controllers share the helper; no server-local fallback remains.
		[Theory]
		[InlineData("ProductController.cs")]
		[InlineData("CouponController.cs")]
		[InlineData("PromotionController.cs")]
		public void Controllers_UseSharedHelper(string file)
		{
			var content = ReadWebFile("Areas", "Admin", "Controllers", file);
			Assert.Contains("OfferTimeZone.ToUtc(", content);
			Assert.Contains("OfferTimeZone.ToLocal(", content);
			Assert.DoesNotContain("TimeZoneInfo.Local", content);
		}

		// 2.1 — the partial shows Argentina wall time; datetime keeps the UTC instant.
		[Fact]
		public void Partial_ConvertsDeadlineToArgentinaTime()
		{
			var partial = ReadWebFile("Views", "Shared", "_DiscountPrice.cshtml");
			Assert.Contains("OfferTimeZone.ToLocal(", partial);
			Assert.Contains("datetime=\"@end.ToString(", partial);
		}

		private static string ReadWebFile(params string[] parts)
		{
			var all = new List<string> { AppContext.BaseDirectory, "..", "..", "..", "..", "VaultShop.Web" };
			all.AddRange(parts);
			return File.ReadAllText(Path.GetFullPath(Path.Combine(all.ToArray())));
		}
	}
}
