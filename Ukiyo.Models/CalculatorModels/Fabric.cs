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
	public class Fabric
	{
		[Key]
		public int Id { get; set; }
		[LocalizedRequired("Fabric name is required.", "El nombre de la tela es obligatorio.")]
		public string Name { get; set; } = string.Empty;
		[LocalizedRequired("Fabric price is required.", "El precio de la tela es obligatorio.")]
		[LocalizedRange(0.01, 1000000, "Please enter a fabric price between 0.01 and 1000000.", "Ingresá un precio de tela entre 0,01 y 1000000.")]
		[DisplayName("Amount Total Payed")]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal Price { get; set; }
		[LocalizedRequired("Fabric quantity is required.", "La cantidad de tela es obligatoria.")]
		[LocalizedRange(0.01, 100000, "Please enter a fabric quantity between 0.01 and 100000.", "Ingresá una cantidad de tela entre 0,01 y 100000.")]
		[DisplayName("Quantity Buyed")]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal Quantity { get; set; }

		
		public string? Description { get; set; }
		

		private decimal _priceMeter;
		[DisplayName("Price Per Meter")]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal PriceMeter
		{
			get => _priceMeter;
			private set => _priceMeter = value;
		}

	}
}
