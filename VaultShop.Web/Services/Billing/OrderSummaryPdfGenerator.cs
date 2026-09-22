using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VaultShop.Models;
using VaultShop.Models.ViewModels;
using VaultShop.Web.Services.Branding;

namespace VaultShop.Web.Services.Billing
{
	public sealed class OrderSummaryPdfGenerator : IOrderSummaryPdfGenerator
	{
		private readonly IStringLocalizer<OrderSummaryPdfGenerator> _localizer;
		private readonly string _publicName;

		static OrderSummaryPdfGenerator()
		{
			QuestPDF.Settings.License = LicenseType.Community;
		}

		public OrderSummaryPdfGenerator(IStringLocalizer<OrderSummaryPdfGenerator> localizer, IOptions<BrandingOptions> branding)
		{
			_localizer = localizer;
			_publicName = branding.Value.PublicName;
		}

		public byte[] Generate(OrderSummaryViewModel summary)
		{
			var culture = CultureInfo.CurrentUICulture;

			return Document.Create(document =>
			{
				document.Page(page =>
				{
					page.Size(PageSizes.A4);
					page.Margin(32);
					page.Content().Element(container => ComposeContent(container, summary, culture));
				});
			}).GeneratePdf();
		}

		private void ComposeContent(IContainer container, OrderSummaryViewModel summary, CultureInfo culture)
		{
			container.Column(column =>
			{
			column.Spacing(8);

			column.Item().Text(_publicName).Bold().FontSize(16);

			column.Item().Row(row =>
			{
				row.RelativeItem().Text(_localizer["OrderSummaryTitle"].Value).Bold().FontSize(20);
				row.AutoItem().Text($"#{summary.OrderId}").FontSize(14);
			});

				column.Item().LineHorizontal(1).LineColor("#CCCCCC");

				column.Item().Row(row =>
				{
					row.RelativeItem().Column(c =>
					{
						c.Item().Text($"{_localizer["OrderDateLabel"].Value}: {summary.OrderDate.ToString("d", culture)}");
						c.Item().Text($"{_localizer["OrderStatusLabel"].Value}: {_localizer[summary.OrderStatus ?? string.Empty].Value}");
						c.Item().Text($"{_localizer["PaymentStatusLabel"].Value}: {_localizer[summary.PaymentStatus ?? string.Empty].Value}");
						c.Item().Text($"{_localizer["PaymentMethodLabel"].Value}: {_localizer[summary.PaymentMethod ?? "Unspecified"].Value}");
						if (summary.PaymentDueDate != default)
						{
							c.Item().Text($"{_localizer["PaymentDueDateLabel"].Value}: {summary.PaymentDueDate.ToString("d", culture)}");
						}
					});
				});

				column.Item().Text(_localizer["CustomerInfoTitle"].Value).Bold().FontSize(14);
				column.Item().Text(summary.CustomerName);
				if (!string.IsNullOrWhiteSpace(summary.CompanyName))
				{
					column.Item().Text(summary.CompanyName);
					if (!string.IsNullOrWhiteSpace(summary.RazonSocial))
						column.Item().Text($"{_localizer["RazonSocialLabel"].Value}: {summary.RazonSocial}");
					if (!string.IsNullOrWhiteSpace(summary.DomicilioFiscal))
						column.Item().Text($"{_localizer["DomicilioFiscalLabel"].Value}: {summary.DomicilioFiscal}");
					if (!string.IsNullOrWhiteSpace(summary.Cuit))
						column.Item().Text($"{_localizer["CuitLabel"].Value}: {summary.Cuit}");
				}

				column.Item().PaddingTop(8).Text(_localizer["ShippingInfoTitle"].Value).Bold().FontSize(14);
				column.Item().Text(summary.ShippingName);
				column.Item().Text(summary.ShippingStreetAddress);
				column.Item().Text($"{summary.ShippingCity}, {summary.ShippingState} {summary.ShippingPostalCode}");
				column.Item().Text(summary.ShippingPhoneNumber);
				if (!string.IsNullOrWhiteSpace(summary.PickupAgencyCode))
				{
					column.Item().PaddingTop(8).Text(_localizer["PickupAgencyTitle"].Value).Bold().FontSize(14);
					if (!string.IsNullOrWhiteSpace(summary.PickupAgencyName))
						column.Item().Text(summary.PickupAgencyName);
					column.Item().Text(summary.PickupAgencyCode);
					if (!string.IsNullOrWhiteSpace(summary.PickupAgencyAddress))
						column.Item().Text(summary.PickupAgencyAddress);
					// ponytail: snapshot is NOT NULL DEFAULT sentinel, so the value renders as-is; fallback only for hand-built VMs
					column.Item().Text($"{_localizer["PickupAgencyHoursLabel"].Value}: {summary.PickupAgencyHours ?? PostalAgency.HoursUnknown}");
				}

				column.Item().PaddingTop(8).Table(table =>
				{
					table.ColumnsDefinition(columns =>
					{
						columns.RelativeColumn(3);
						columns.RelativeColumn(2);
						columns.RelativeColumn(1);
						columns.RelativeColumn(2);
					});

					table.Header(header =>
					{
						header.Cell().Element(CellStyle).Text(_localizer["ProductHeader"].Value);
						header.Cell().Element(CellStyle).AlignRight().Text(_localizer["UnitPriceHeader"].Value);
						header.Cell().Element(CellStyle).AlignCenter().Text(_localizer["QuantityHeader"].Value);
						header.Cell().Element(CellStyle).AlignRight().Text(_localizer["LineTotalHeader"].Value);
					});

					foreach (var item in summary.Items)
					{
						table.Cell().Element(CellStyle).Text(item.ProductName);
						table.Cell().Element(CellStyle).AlignRight().Text(item.UnitPrice.ToString("c", culture));
						table.Cell().Element(CellStyle).AlignCenter().Text(item.Quantity.ToString());
						table.Cell().Element(CellStyle).AlignRight().Text(item.LineTotal.ToString("c", culture));
					}
				});

				column.Item().AlignRight().Text($"{_localizer["TotalLabel"].Value}: {summary.OrderTotal.ToString("c", culture)}").Bold().FontSize(14);

				column.Item().PaddingTop(16).LineHorizontal(1).LineColor("#999999");
				column.Item().Text(_localizer["NonFiscalLegend"].Value).Italic().FontSize(10);
			});
		}

		private static IContainer CellStyle(IContainer container) =>
			container.BorderBottom(1).BorderColor("#E0E0E0").PaddingVertical(4);
	}
}
