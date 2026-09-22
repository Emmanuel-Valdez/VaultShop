namespace VaultShop.Models.ViewModels
{
	public class OrderSummaryViewModel
	{
		public int OrderId { get; set; }
		public DateTime OrderDate { get; set; }
		public string? OrderStatus { get; set; }
		public string? PaymentStatus { get; set; }
		public string? PaymentMethod { get; set; }
		public DateOnly PaymentDueDate { get; set; }

		public string CustomerName { get; set; } = string.Empty;

		public string? CompanyName { get; set; }
		public string? RazonSocial { get; set; }
		public string? DomicilioFiscal { get; set; }
		public string? Cuit { get; set; }

		public string ShippingName { get; set; } = string.Empty;
		public string ShippingStreetAddress { get; set; } = string.Empty;
		public string ShippingCity { get; set; } = string.Empty;
		public string ShippingState { get; set; } = string.Empty;
		public string ShippingPostalCode { get; set; } = string.Empty;
		public string ShippingPhoneNumber { get; set; } = string.Empty;

		// correo-argentino pickup snapshot: null for pre-change orders
		public string? DeliveryType { get; set; }
		public string? PickupAgencyCode { get; set; }
		public string? PickupAgencyName { get; set; }
		public string? PickupAgencyAddress { get; set; }
		public string? PickupAgencyHours { get; set; }

		public List<OrderSummaryItemViewModel> Items { get; set; } = new();
		public decimal OrderTotal { get; set; }
	}

	public class OrderSummaryItemViewModel
	{
		public string ProductName { get; set; } = string.Empty;
		public decimal UnitPrice { get; set; }
		public int Quantity { get; set; }
		public decimal LineTotal => UnitPrice * Quantity;
	}
}
