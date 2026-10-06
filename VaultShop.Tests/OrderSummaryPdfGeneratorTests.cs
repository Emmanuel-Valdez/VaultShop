using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using VaultShop.Models.ViewModels;
using VaultShop.Web.Services.Billing;
using VaultShop.Web.Services.Branding;

namespace VaultShop.Web.Tests
{
	public class OrderSummaryPdfGeneratorTests
	{
		[Fact]
		public void Generate_WithValidSummary_ReturnsPdfWithMagicHeader()
		{
			var localizerMock = new Mock<IStringLocalizer<OrderSummaryPdfGenerator>>();
			localizerMock
				.Setup(x => x[It.IsAny<string>()])
				.Returns((string name) => new LocalizedString(name, name));

			var branding = Options.Create(new BrandingOptions { PublicName = "TestStore" });
			var generator = new OrderSummaryPdfGenerator(localizerMock.Object, branding);
			var summary = CreateSampleSummary();

			var pdf = generator.Generate(summary);

			Assert.NotNull(pdf);
			Assert.True(pdf.Length > 100, "Generated PDF should contain more than a trivial number of bytes.");
			Assert.Equal((byte)'%', pdf[0]);
			Assert.Equal((byte)'P', pdf[1]);
			Assert.Equal((byte)'D', pdf[2]);
			Assert.Equal((byte)'F', pdf[3]);
		}

		[Fact]
		public void Generate_WithCompanyFiscalFieldsAndDueDate_ProducesPdf()
		{
			var generator = CreateGenerator();
			var summary = CreateFiscalSummary();

			var pdf = generator.Generate(summary);

			Assert.NotNull(pdf);
			Assert.Equal((byte)'%', pdf[0]);
			Assert.True(pdf.Length > 100);
		}

		[Fact]
		public void Generate_WithoutCompany_ProducesPdf()
		{
			var generator = CreateGenerator();
			var summary = CreateSampleSummary();

			var pdf = generator.Generate(summary);

			Assert.NotNull(pdf);
			Assert.Equal((byte)'%', pdf[0]);
			Assert.True(pdf.Length > 100);
		}

		[Fact]
		public void Generate_PickupOrderWithHours_ProducesPdf()
		{
			var generator = CreateGenerator();
			var summary = CreateSampleSummary();
			summary.DeliveryType = "S";
			summary.PickupAgencyCode = "CEN01";
			summary.PickupAgencyName = "Sucursal Centro";
			summary.PickupAgencyAddress = "Centro 1";
			summary.PickupAgencyHours = "LUN A VIE 9 A 18";

			var pdf = generator.Generate(summary);

			Assert.NotNull(pdf);
			Assert.Equal((byte)'%', pdf[0]);
			Assert.True(pdf.Length > 100);
			Assert.Contains("LUN A VIE 9 A 18", PdfTextExtractor.Extract(pdf));
		}

		[Fact]
		public void Generate_PickupOrderWithoutHours_ProducesPdf()
		{
			var generator = CreateGenerator();
			var summary = CreateSampleSummary();
			summary.DeliveryType = "S";
			summary.PickupAgencyCode = "CEN01";
			summary.PickupAgencyName = "Sucursal Centro";
			summary.PickupAgencyHours = null;

			var pdf = generator.Generate(summary);

			Assert.NotNull(pdf);
			Assert.Equal((byte)'%', pdf[0]);
			Assert.True(pdf.Length > 100);
			Assert.Contains("no informa", PdfTextExtractor.Extract(pdf));
		}

		private static OrderSummaryPdfGenerator CreateGenerator()
		{
			var localizerMock = new Mock<IStringLocalizer<OrderSummaryPdfGenerator>>();
			localizerMock
				.Setup(x => x[It.IsAny<string>()])
				.Returns((string name) => new LocalizedString(name, name));
			var branding = Options.Create(new BrandingOptions { PublicName = "TestStore" });
			return new OrderSummaryPdfGenerator(localizerMock.Object, branding);
		}

		private static OrderSummaryViewModel CreateFiscalSummary()
		{
			return new OrderSummaryViewModel
			{
				OrderId = 99,
				OrderDate = new DateTime(2026, 8, 21, 14, 0, 0, DateTimeKind.Utc),
				OrderStatus = "Approved",
				PaymentStatus = "DelayedPayment",
				PaymentMethod = "BankTransfer",
				PaymentDueDate = DateOnly.FromDateTime(new DateTime(2026, 9, 1)),
				CustomerName = "Maria Lopez",
				CompanyName = "Textiles SA",
				RazonSocial = "Textiles SA SRL",
				DomicilioFiscal = "Av. Corrientes 1234, CABA",
				Cuit = "30-71234567-9",
				ShippingName = "Maria Lopez",
				ShippingStreetAddress = "Av. Corrientes 1234",
				ShippingCity = "CABA",
				ShippingState = "CABA",
				ShippingPostalCode = "1043",
				ShippingPhoneNumber = "+54 11 8765-4321",
				Items =
				[
					new() { ProductName = "Tela Algodón", UnitPrice = 2500m, Quantity = 2 }
				],
				OrderTotal = 5000m
			};
		}

		private static OrderSummaryViewModel CreateSampleSummary()
		{
			return new OrderSummaryViewModel
			{
				OrderId = 42,
				OrderDate = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc),
				OrderStatus = "Pending",
				PaymentStatus = "Pending",
				PaymentMethod = "Stripe",
				CustomerName = "Juan Perez",
				ShippingName = "Juan Perez",
				ShippingStreetAddress = "Calle Falsa 123",
				ShippingCity = "Buenos Aires",
				ShippingState = "Buenos Aires",
				ShippingPostalCode = "1234",
				ShippingPhoneNumber = "+54 11 1234-5678",
				Items =
				[
					new() { ProductName = "Remera Básica", UnitPrice = 1500m, Quantity = 2 },
					new() { ProductName = "Gorra Classic", UnitPrice = 800m, Quantity = 1 }
				],
				OrderTotal = 3800m
			};
		}
	}
}
