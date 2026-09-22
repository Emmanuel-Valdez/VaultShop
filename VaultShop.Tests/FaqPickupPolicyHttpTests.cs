using Microsoft.AspNetCore.Mvc.Testing;

namespace VaultShop.Web.Tests
{
	public class FaqPickupPolicyHttpTests
	{
		[Theory]
		[InlineData("es-AR", "retiro en sucursal de Correo Argentino", "5 días hábiles")]
		[InlineData("en-US", "Correo Argentino branch pickup", "5 business days")]
		public async Task FAQs_RendersPickupPolicyItem(string culture, string questionFragment, string policyFragment)
		{
			using var factory = new CustomWebApplicationFactory();
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

			var html = await client.GetStringAsync($"/{culture}/Customer/Home/FAQs");

			Assert.Contains(questionFragment, html, StringComparison.OrdinalIgnoreCase);
			Assert.Contains(policyFragment, html, StringComparison.OrdinalIgnoreCase);
		}
	}
}
