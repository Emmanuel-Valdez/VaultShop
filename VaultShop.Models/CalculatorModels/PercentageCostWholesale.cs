using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UkiyoDesigns.Models.Validation;

namespace UkiyoDesigns.Models.CalculatorModels
{
	public class PercentageCostWholesale
	{
		[Key]
		public int Id { get; set; }

		[LocalizedRequired("Wholesale percentage cost name is required.", "El nombre del costo porcentual mayorista es obligatorio.")]
		public string Name { get; set; } = string.Empty;

		[LocalizedRequired("Wholesale percentage is required.", "El porcentaje mayorista es obligatorio.")]
		[LocalizedRange(0, 100, "Please enter a wholesale percentage between 0 and 100.", "Ingresá un porcentaje mayorista entre 0 y 100.")]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal Percentage { get; set; }

		public string? Description { get; set; }
	}
}
