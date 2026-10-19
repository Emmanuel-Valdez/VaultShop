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

		[Theory]
		[InlineData("es-AR", "¿Se pueden combinar los descuentos?", "El descuento por medio de pago")]
		[InlineData("en-US", "Can discounts be combined?", "payment-method discount does stack")]
		public async Task FAQs_RendersStackingItem(string culture, string questionFragment, string answerFragment)
		{
			using var factory = new CustomWebApplicationFactory();
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

			var html = await client.GetStringAsync($"/{culture}/Customer/Home/FAQs");

			Assert.Contains(questionFragment, html, StringComparison.OrdinalIgnoreCase);
			Assert.Contains(answerFragment, html, StringComparison.OrdinalIgnoreCase);
		}

		[Theory]
		[InlineData("es-AR", "Correo Argentino", "unidad postal", "en moto", "Microcentro")]
		[InlineData("en-US", "Correo Argentino", "postal unit", "motorcycle home delivery", "Microcentro")]
		public async Task FAQs_RendersShippingMethodsCopy(string culture, string correoFragment, string postalFragment, string motorcycleFragment, string microcentroFragment)
		{
			using var factory = new CustomWebApplicationFactory();
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

			var html = await client.GetStringAsync($"/{culture}/Customer/Home/FAQs");

			Assert.Contains(correoFragment, html, StringComparison.OrdinalIgnoreCase);
			Assert.Contains(postalFragment, html, StringComparison.OrdinalIgnoreCase);
			Assert.Contains(motorcycleFragment, html, StringComparison.OrdinalIgnoreCase);
			Assert.Contains(microcentroFragment, html, StringComparison.OrdinalIgnoreCase);
		}

		[Theory]
		[InlineData("es-AR", "no tiene costo", "sin importar la cantidad", "mayoristas no pagan")]
		[InlineData("en-US", "free shipping", "regardless of the number", "Wholesale")]
		public async Task FAQs_RendersFreePaidShippingRule(string culture, string freeFragment, string onceFragment, string wholesaleFragment)
		{
			using var factory = new CustomWebApplicationFactory();
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

			var html = await client.GetStringAsync($"/{culture}/Customer/Home/FAQs");

			Assert.Contains(freeFragment, html, StringComparison.OrdinalIgnoreCase);
			Assert.Contains(onceFragment, html, StringComparison.OrdinalIgnoreCase);
			Assert.Contains(wholesaleFragment, html, StringComparison.OrdinalIgnoreCase);
		}
		[Theory]
		[InlineData("es-AR", "Visa", "Mastercard", "Transferencia bancaria")]
		[InlineData("en-US", "Visa", "Mastercard", "Bank transfer")]
		public async Task FAQs_RendersAcceptedPaymentMethods(string culture, string visas, string mastercard, string transferFragment)
		{
			using var factory = new CustomWebApplicationFactory();
			var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

			var html = await client.GetStringAsync($"/{culture}/Customer/Home/FAQs");

			Assert.Contains(visas, html, StringComparison.OrdinalIgnoreCase);
			Assert.Contains(mastercard, html, StringComparison.OrdinalIgnoreCase);
			Assert.Contains(transferFragment, html, StringComparison.OrdinalIgnoreCase);
			// The bank-transfer discount is promotion-driven, not a guaranteed FAQ promise.
			Assert.DoesNotContain("10%", html);
		}
	}
}
