namespace VaultShop.Web.Tests
{
	// oferta-decimales-variantes 4.2 — the description belongs on product detail only.
	public class StorefrontDescriptionTests
	{
		[Theory]
		[InlineData("Cart")]
		[InlineData("Favorite")]
		public void CartAndFavorites_DoNotRenderDescription(string controller)
		{
			Assert.DoesNotContain("Product.Description", ReadCustomerView(controller, "Index.cshtml"));
		}

		[Fact]
		public void ProductDetail_StillRendersDescription()
		{
			Assert.Contains("@Html.Raw(Model.Product.Description)", ReadCustomerView("Home", "Details.cshtml"));
		}

		private static string ReadCustomerView(string controller, string file)
		{
			var path = Path.GetFullPath(Path.Combine(
				AppContext.BaseDirectory, "..", "..", "..", "..", "VaultShop.Web",
				"Areas", "Customer", "Views", controller, file));
			return File.ReadAllText(path);
		}
	}
}