using System.Globalization;

namespace VaultShop.Web.Tests
{
	// oferta-decimales-variantes 2.1 + 2.2 — admin discount entry follows the request
	// culture and shows localized enum labels.
	public class AdminDiscountEntryTests
	{
		// 2.1 — decimal inputs post raw text so the culture binder parses the separator.
		[Theory]
		[InlineData("Product", "Product.SaleRetailPrice")]
		[InlineData("Product", "Product.SaleWholesalePrice")]
		[InlineData("Coupon", "Value")]
		[InlineData("Coupon", "MinSubtotal")]
		[InlineData("Promotion", "Promotion.GetDiscountPercent")]
		[InlineData("Promotion", "Promotion.DiscountPercent")]
		public void DecimalInput_UsesTextWithDecimalInputMode(string view, string field)
		{
			var tag = FindInputTag(view, field);
			Assert.Contains("inputmode=\"decimal\"", tag);
			Assert.DoesNotContain("type=\"number\"", tag);
		}

		// 2.1 — integer inputs stay numeric.
		[Theory]
		[InlineData("Coupon", "MaxUses")]
		[InlineData("Promotion", "Promotion.BuyQty")]
		[InlineData("Promotion", "Promotion.GetQty")]
		[InlineData("Product", "Product.StockQuantity")]
		[InlineData("Product", "Product.MaxExpectation")]
		public void IntegerInput_StaysNumeric(string view, string field)
		{
			var tag = FindInputTag(view, field);
			Assert.Contains("type=\"number\"", tag);
		}

		// 2.1 — es-AR binds the comma; a dot is never eight thousand (client validator
		// rejects it, see _ValidationScriptsPartial); en-US binds the dot.
		[Fact]
		public void EsAr_AcceptsComma()
		{
			var esAr = CultureInfo.GetCultureInfo("es-AR");
			Assert.Equal(8000.50m, decimal.Parse("8000,50", NumberStyles.Number, esAr));
		}

		[Fact]
		public void EsAr_DotIsNotEightThousand()
		{
			var esAr = CultureInfo.GetCultureInfo("es-AR");
			Assert.NotEqual(8000.50m, decimal.Parse("8000.50", NumberStyles.Number, esAr));
			var partial = ReadWebFile("Views", "Shared", "_ValidationScriptsPartial.cshtml");
			Assert.Contains("if (value.indexOf(\".\") !== -1)", partial);
		}

		[Fact]
		public void EnUs_AcceptsDot()
		{
			var enUs = CultureInfo.GetCultureInfo("en-US");
			Assert.Equal(8000.50m, decimal.Parse("8000.50", NumberStyles.Number, enUs));
		}

		// 2.2 — coupon type labels in both cultures; stored enum values unchanged.
		[Theory]
		[InlineData("es-AR", "TypePercent", "Porcentual")]
		[InlineData("es-AR", "TypeFixedAmount", "Monto fijo")]
		[InlineData("en-US", "TypePercent", "Percent")]
		[InlineData("en-US", "TypeFixedAmount", "Fixed amount")]
		public void CouponType_LabelsAreLocalized(string culture, string key, string expected)
		{
			Assert.Equal(expected, CouponResources.GetString(key, CultureInfo.GetCultureInfo(culture)));
		}

		// 2.2 — promotion kind and scope labels in both cultures.
		[Theory]
		[InlineData("es-AR", "KindBxGy", "Llevá X, pagá Y")]
		[InlineData("es-AR", "KindPercentOff", "Porcentaje")]
		[InlineData("es-AR", "KindPaymentMethodDiscount", "Descuento por medio de pago")]
		[InlineData("es-AR", "ScopeStore", "Tienda")]
		[InlineData("es-AR", "ScopeProduct", "Producto")]
		[InlineData("es-AR", "ScopeCategory", "Categoría")]
		[InlineData("es-AR", "ScopeKeyword", "Colección")]
		[InlineData("en-US", "KindBxGy", "Buy X, pay Y")]
		[InlineData("en-US", "KindPercentOff", "Percent off")]
		[InlineData("en-US", "KindPaymentMethodDiscount", "Payment method discount")]
		[InlineData("en-US", "ScopeStore", "Store")]
		[InlineData("en-US", "ScopeProduct", "Product")]
		[InlineData("en-US", "ScopeCategory", "Category")]
		[InlineData("en-US", "ScopeKeyword", "Collection")]
		public void PromotionKindAndScope_LabelsAreLocalized(string culture, string key, string expected)
		{
			Assert.Equal(expected, PromotionResources.GetString(key, CultureInfo.GetCultureInfo(culture)));
		}

		// 2.2 — the upsert forms build selects from those localizer entries.
		[Fact]
		public void CouponForm_UsesLocalizedTypeOptions()
		{
			var view = ReadAdminView("Coupon", "Upsert.cshtml");
			Assert.DoesNotContain("GetEnumSelectList", view);
			Assert.Contains("@Localizer[\"TypePercent\"]", view);
			Assert.Contains("@Localizer[\"TypeFixedAmount\"]", view);
			Assert.Contains("value=\"@nameof(CouponDiscountType.Percent)\"", view);
			Assert.Contains("value=\"@nameof(CouponDiscountType.FixedAmount)\"", view);
		}

		[Fact]
		public void PromotionForm_UsesLocalizedKindAndScopeOptions()
		{
			var view = ReadAdminView("Promotion", "Upsert.cshtml");
			Assert.DoesNotContain("GetEnumSelectList", view);
			foreach (var key in new[] { "KindBxGy", "KindPercentOff", "KindPaymentMethodDiscount", "ScopeStore", "ScopeProduct", "ScopeCategory", "ScopeKeyword" })
				Assert.Contains($"@Localizer[\"{key}\"]", view);
			// ponytail: section toggles match on enum names via option values, not localized text.
			Assert.Contains("var kind = kindEl.value;", view);
			Assert.Contains("var scope = scopeEl.value;", view);
		}

		private static readonly System.Resources.ResourceManager CouponResources = new(
			"VaultShop.Web.Resources.Areas.Admin.Views.Coupon.Upsert",
			typeof(VaultShop.Web.Areas.Admin.Controllers.CouponController).Assembly);

		private static readonly System.Resources.ResourceManager PromotionResources = new(
			"VaultShop.Web.Resources.Areas.Admin.Views.Promotion.Upsert",
			typeof(VaultShop.Web.Areas.Admin.Controllers.PromotionController).Assembly);

		private static string FindInputTag(string view, string field)
		{
			var content = ReadAdminView(view, "Upsert.cshtml");
			var marker = $"asp-for=\"{field}\"";
			var index = content.IndexOf(marker, StringComparison.Ordinal);
			Assert.True(index >= 0, $"No input for {field} in {view}/Upsert.cshtml.");
			var tagStart = content.LastIndexOf('<', index);
			var tagEnd = content.IndexOf('>', index);
			return content.Substring(tagStart, tagEnd - tagStart + 1);
		}

		private static string ReadAdminView(string controller, string file) =>
			ReadWebFile("Areas", "Admin", "Views", controller, file);

		private static string ReadWebFile(params string[] parts)
		{
			var all = new List<string> { AppContext.BaseDirectory, "..", "..", "..", "..", "VaultShop.Web" };
			all.AddRange(parts);
			return File.ReadAllText(Path.GetFullPath(Path.Combine(all.ToArray())));
		}
	}
}
