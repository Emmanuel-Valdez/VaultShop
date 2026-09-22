using System.Text.RegularExpressions;
using Match = System.Text.RegularExpressions.Match;
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
			Assert.Contains("LUN A VIE 9 A 18", ExtractPdfText(pdf));
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
			Assert.Contains("no informa", ExtractPdfText(pdf));
		}

		private static string ExtractPdfText(byte[] pdf)
		{
			// ponytail: test-only extractor — inflates every stream, collects glyph codes from every
			// text-showing operator, then decodes them with each embedded font's ToUnicode map and
			// concatenates the results. A text run that lives in one font decodes cleanly under that
			// font's map (garbage under the others), so the asserted substring still shows up exactly once.
			var content = System.Text.Encoding.Latin1.GetString(pdf);
			var inflated = new System.Text.StringBuilder();
			var stream = 0;
			while (stream < content.Length)
			{
				var start = content.IndexOf("stream", stream, StringComparison.Ordinal);
				if (start < 0) break;
				start += 6;
				if (content[start] == '\r') start++;
				if (content[start] == '\n') start++;
				var end = content.IndexOf("endstream", start, StringComparison.Ordinal);
				if (end < 0) break;
				inflated.Append(TryInflate(content[start..end]));
				stream = end + 9;
			}
			var codes = new List<string>();
			foreach (Match match in TextShowRegex.Matches(inflated.ToString()))
			{
				foreach (Match hex in HexCodeRegex.Matches(match.Groups[1].Value))
				{
					var value = hex.Groups[1].Value;
					for (var i = 0; i < value.Length; i += 4)
					{
						codes.Add(value.Substring(i, Math.Min(4, value.Length - i)));
					}
				}
			}
			var text = new System.Text.StringBuilder();
			foreach (var map in BuildUnicodeMaps(inflated.ToString()))
			{
				foreach (var code in codes)
				{
					if (map.TryGetValue(Convert.ToInt32(code, 16), out var ch))
					{
						text.Append(ch);
					}
				}
				text.Append('\n');
			}
			return text.ToString();
		}

		// glyph codes are 2 bytes in the Identity-H <0000> <FFFF> space; runs like <006F0075> pack several
		private static readonly Regex TextShowRegex = new(@"\[(?<array>[0-9A-Fa-f<>\s.\-]+)\]\s*TJ", RegexOptions.Compiled);
		private static readonly Regex HexCodeRegex = new(@"<([0-9A-Fa-f]+)>", RegexOptions.Compiled);

		private static List<Dictionary<int, char>> BuildUnicodeMaps(string inflated)
		{
			var maps = new List<Dictionary<int, char>>();
			foreach (Match cmap in Regex.Matches(inflated, @"begincmap[\s\S]*?endcmap"))
			{
				var map = new Dictionary<int, char>();
				foreach (Match block in Regex.Matches(cmap.Value, @"beginbf(char|range)[\s\S]*?endbf(char|range)"))
				{
					if (block.Value.Contains("beginbfrange"))
					{
						// beginbfrange: (<srcStart>) (<srcEnd>) (<dstStart>) — dst grows with the source span
						foreach (Match triple in Regex.Matches(block.Value,
							@"<([0-9A-Fa-f]{2,4})>\s*<([0-9A-Fa-f]{2,4})>\s*<([0-9A-Fa-f]{2,4})>"))
						{
							var code = Convert.ToInt32(triple.Groups[1].Value, 16);
							var count = Convert.ToInt32(triple.Groups[2].Value, 16) - code;
							var start = Convert.ToInt32(triple.Groups[3].Value, 16);
							for (var offset = 0; offset <= count; offset++)
							{
								map[code + offset] = (char)(start + offset);
							}
						}
					}
					else
					{
						foreach (Match pair in Regex.Matches(block.Value, @"<([0-9A-Fa-f]{2,4})>\s*<([0-9A-Fa-f]{2,4})>"))
						{
							map[Convert.ToInt32(pair.Groups[1].Value, 16)] = (char)Convert.ToInt32(pair.Groups[2].Value, 16);
						}
					}
				}
				maps.Add(map);
			}
			return maps;
		}

		private static string TryInflate(string raw)
		{
			var bytes = System.Text.Encoding.Latin1.GetBytes(raw);
			try
			{
				using var input = new System.IO.MemoryStream(bytes);
				using var zlib = new System.IO.Compression.ZLibStream(input, System.IO.Compression.CompressionMode.Decompress);
				using var output = new System.IO.MemoryStream();
				zlib.CopyTo(output);
				return System.Text.Encoding.Latin1.GetString(output.ToArray());
			}
			catch (System.IO.InvalidDataException)
			{
				return raw;
			}
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
					new() { ProductName = "Remera B�sica", UnitPrice = 1500m, Quantity = 2 },
					new() { ProductName = "Gorra Classic", UnitPrice = 800m, Quantity = 1 }
				],
				OrderTotal = 3800m
			};
		}
	}
}
