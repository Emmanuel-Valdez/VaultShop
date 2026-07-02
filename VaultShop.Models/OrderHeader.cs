using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UkiyoDesigns.Models.Validation;

namespace UkiyoDesigns.Models
{
	public class OrderHeader
	{
		public int Id { get; set; }
		public string ApplicationUserId { get; set; } = string.Empty;
		[ForeignKey("ApplicationUserId")]
		[ValidateNever]
		public ApplicationUser ApplicationUser { get; set; } = null!;

		public int? CompanyId { get; set; }
		[ForeignKey("CompanyId")]
		[ValidateNever]
		public Company? Company { get; set; }

		public DateTime OrderDate { get; set; }
		public DateTime ShippingDate { get; set; }

		[LocalizedRange(0, 100000000, "Please enter an order total between 0 and 100000000.", "Ingresá un total de orden entre 0 y 100000000.")]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal OrderTotal { get; set; }

		public string? OrderStatus { get; set; }
		public string? PaymentStatus { get; set; }
		public string? TrackingNumber { get; set; }
		public string? Carrier { get; set; }

		public DateTime PaymentDate { get; set; }
		public DateOnly PaymentDueDate { get; set; }

		public string? SessionId { get; set; }
		public string? PaymentIntentId { get; set; }

		[LocalizedRequired("Name is required.", "El nombre es obligatorio.")]
		public string Name { get; set; } = string.Empty;
		[Display(Name = "Street Address")]
		[LocalizedRequired("Street address is required.", "La dirección es obligatoria.")]
		public string StreetAddress { get; set; } = string.Empty;
		[LocalizedRequired("City is required.", "La ciudad es obligatoria.")]
		public string City { get; set; } = string.Empty;
		[LocalizedRequired("State is required.", "La provincia es obligatoria.")]
		public string State { get; set; } = string.Empty;
		[LocalizedRequired("Postal code is required.", "El código postal es obligatorio.")]
		[Display(Name = "Postal Code")]
		public string PostalCode { get; set; } = string.Empty;
		[LocalizedRequired("Phone number is required.", "El teléfono es obligatorio.")]
		[Display(Name = "Phone Number")]
		public string PhoneNumber { get; set; } = string.Empty;

	}
}
