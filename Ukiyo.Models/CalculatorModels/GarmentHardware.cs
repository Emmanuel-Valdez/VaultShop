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
	public class GarmentHardware
	{
		[Key]
		public int Id { get; set; }
		[LocalizedRequired("Hardware name is required.", "El nombre del herraje es obligatorio.")]
		public string Name { get; set; } = string.Empty;
		[LocalizedRequired("Hardware price is required.", "El precio del herraje es obligatorio.")]
		[DisplayName("Amount Total Payed")]
		[LocalizedRange(0.01, 1000000, "Please enter a hardware price between 0.01 and 1000000.", "Ingresá un precio de herraje entre 0,01 y 1000000.")]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal Price { get; set; }
		[LocalizedRequired("Hardware quantity is required.", "La cantidad de herrajes es obligatoria.")]
		[LocalizedRange(0.01, 100000, "Please enter a hardware quantity between 0.01 and 100000.", "Ingresá una cantidad de herrajes entre 0,01 y 100000.")]
		[DisplayName("Quantity Buyed")]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal Quantity { get; set; }
		public string? Description { get; set; }


		private decimal _unitPrice;
		[Column(TypeName = "decimal(18, 2)")]
		[DisplayName("Unit Price")]
		public decimal UnitPrice
		{
			get => _unitPrice;
			private set => _unitPrice = value;
		}

	}
}
