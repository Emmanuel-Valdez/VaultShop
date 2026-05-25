using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UkiyoDesigns.Models.Validation;

namespace UkiyoDesigns.Models.CalculatorModels
{
	public class PercentageCost
	{
		[Key]
		public int Id { get; set; }
		[LocalizedRequired("Percentage cost name is required.", "El nombre del costo porcentual es obligatorio.")]
		public string Name { get; set; } = string.Empty;
		[LocalizedRequired("Percentage is required.", "El porcentaje es obligatorio.")]
		[LocalizedRange(0, 100, "Please enter a percentage between 0 and 100.", "Ingresá un porcentaje entre 0 y 100.")]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal Percentage { get; set; }
        public string? Description { get; set; }
	}
}
