using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VaultShop.DataAccess.Data;
using VaultShop.Models;

namespace VaultShop.Web.Tests;

// home-banners A4: promo/coupon DataTable columns must speak the page culture.
public class PromoCouponLocalizationHttpTests
{
	[Fact]
	public async Task GetAll_LocalizedEs_ReturnsLocalizedEnumLabels()
	{
		using var factory = new CustomWebApplicationFactory();
		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);
		using (var scope = factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			db.Promotions.Add(new Promotion { Name = "Verano", Kind = PromotionKind.PercentOff, Scope = PromotionScope.Store });
			db.Coupons.Add(new Coupon { Code = "VERANO10", DiscountType = CouponDiscountType.Percent, Value = 10 });
			db.Coupons.Add(new Coupon { Code = "FIJO15", DiscountType = CouponDiscountType.FixedAmount, Value = 15 });
			db.SaveChanges();
		}

		var promo = await client.GetAsync("/es-AR/Admin/Promotion/GetAll");
		var promoJson = await promo.Content.ReadAsStringAsync();
		var coupon = await client.GetAsync("/es-AR/Admin/Coupon/GetAll");
		var couponJson = await coupon.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.OK, promo.StatusCode);
		Assert.Contains("Porcentaje de descuento", promoJson);
		Assert.Contains("Tienda", promoJson);
		Assert.Equal(HttpStatusCode.OK, coupon.StatusCode);
		Assert.Contains("Porcentaje", couponJson);
		Assert.Contains("Monto fijo", couponJson);
	}
}