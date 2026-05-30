using System.ComponentModel.DataAnnotations;
using UkiyoDesigns.Models.Validation;

namespace UkiyoDesigns.Models
{
	public class Company
	{
		[Key]
		public int Id { get; set; }
		[LocalizedRequired("Company name is required.", "El nombre de la empresa es obligatorio.")]
		public string Name { get; set; } = string.Empty;
		[Display(Name = "Street Address")]
		public string? StreetAddress { get; set; }
		public string? City { get; set; }
		public string? State { get; set; }
		[Display(Name = "Postal Code")]
		public string? PostalCode { get; set; }
		[Display(Name = "Phone Number")]
		public string? PhoneNumber { get; set; }
		public bool IsDeleted { get; set; } = false;

	}
}
