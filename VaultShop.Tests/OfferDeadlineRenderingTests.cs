using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VaultShop.DataAccess.Data;
using VaultShop.Models;
using VaultShop.Web.Services.Pricing;

namespace VaultShop.Web.Tests;

// oferta-huso-horario-ar 2.1: the deadline shows Argentina wall time with the
// UTC instant in <time datetime>; no end date renders no clock line.
public class OfferDeadlineRenderingTests
{
	[Fact]
	public async Task HomeIndex_OfferDeadline_ShowsArgentinaTime()
	{
		// Whole-hour end stays active (now is inside the window) and formats stably.
		var endUtc = new DateTime(
			DateTime.UtcNow.AddDays(1).Ticks / TimeSpan.TicksPerHour * TimeSpan.TicksPerHour,
			DateTimeKind.Utc);
		using var factory = new CustomWebApplicationFactory();
		Seed(factory, saleToUtc: endUtc);
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

		var body = await client.GetStringAsync("/en-US/Customer/Home/Index");

		var localEnd = OfferTimeZone.ToLocal(endUtc)!.Value;
		Assert.Contains($"datetime=\"{endUtc:yyyy-MM-ddTHH:mmZ}\"", body);
		Assert.Contains($"Valid until {localEnd:MM/dd HH:mm}", body);
	}

	[Fact]
	public async Task HomeIndex_OfferWithoutEnd_RendersNoClock()
	{
		using var factory = new CustomWebApplicationFactory();
		Seed(factory, saleToUtc: null);
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

		var body = await client.GetStringAsync("/en-US/Customer/Home/Index");

		Assert.Contains("badge bg-success\">Offer</span>", body);
		Assert.DoesNotContain("bi-clock", body);
	}

	private static void Seed(CustomWebApplicationFactory factory, DateTime? saleToUtc)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var category = new Category { Name = "Test Category", AvgShippingCost = 100m };
		db.Categories.Add(category);
		db.SaveChanges();

		db.Products.Add(new Product
		{
			Name = "Offer Product",
			Description = "Test",
			MaxExpectation = 10,
			Category = category,
			ListPrice = 100m,
			FinalRetailPrice = 100m,
			FinalWholesalePrice = 70m,
			SaleRetailPrice = 80m,
			SaleFromUtc = DateTime.UtcNow.AddDays(-1),
			SaleToUtc = saleToUtc,
			IsAvailableInStore = true,
			IsDeleted = false,
			StockQuantity = 5,
		});
		db.SaveChanges();
	}
}
