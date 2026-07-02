using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VaultShop.Models.Validation;

namespace VaultShop.Models.CalculatorModels
{
	public class FixedCost
	{
		[Key]
		public int Id { get; set; }
		[LocalizedRequired("Fixed cost name is required.", "El nombre del costo fijo es obligatorio.")]
		public string Name { get; set; } = string.Empty;
		[LocalizedRequired("Fixed cost amount is required.", "El monto del costo fijo es obligatorio.")]
		[DisplayName("Approximate monthly fixed cost")]
		[LocalizedRange(0, 10000000, "Please enter a fixed cost between 0 and 10000000.", "Ingresá un costo fijo entre 0 y 10000000.")]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal Cost { get; set; }
        public string? Description { get; set; }
	}
}
