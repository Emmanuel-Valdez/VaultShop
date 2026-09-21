using System.ComponentModel.DataAnnotations;
using VaultShop.Models.Validation;

namespace VaultShop.Models
{
	public class Company
	{
		[Key]
		public int Id { get; set; }
		[LocalizedRequired("Company name is required.", "El nombre de la empresa es obligatorio.")]
		public string Name { get; set; } = string.Empty;
		[Display(Name = "Street Address")]
		[LocalizedRequired("Street address is required.", "La dirección es obligatoria.")]
		public string StreetAddress { get; set; } = string.Empty;
		[LocalizedRequired("City is required.", "La localidad es obligatoria.")]
		public string City { get; set; } = string.Empty;
		[LocalizedRequired("State is required.", "La provincia es obligatoria.")]
		public string State { get; set; } = string.Empty;
		[Display(Name = "Postal Code")]
		[LocalizedRequired("Postal code is required.", "El código postal es obligatorio.")]
		public string PostalCode { get; set; } = string.Empty;
		[Display(Name = "Phone Number")]
		[LocalizedRequired("Phone number is required.", "El teléfono es obligatorio.")]
		public string PhoneNumber { get; set; } = string.Empty;

		[Display(Name = "Razón social")]
		[LocalizedRequired("Legal name is required.", "La razón social es obligatoria.")]
		public string RazonSocial { get; set; } = string.Empty;
		[Display(Name = "Domicilio fiscal")]
		[LocalizedRequired("Tax address is required.", "El domicilio fiscal es obligatorio.")]
		public string DomicilioFiscal { get; set; } = string.Empty;
		[Display(Name = "CUIT")]
		public string? Cuit { get; set; }

		public bool IsDeleted { get; set; } = false;

	}
}
