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
	public class Packaging
	{
		[Key]
		public int Id { get; set; }
		[LocalizedRequired("Packaging name is required.", "El nombre del packaging es obligatorio.")]
		public string Name { get; set; } = string.Empty;
		[LocalizedRequired("Packaging price is required.", "El precio del packaging es obligatorio.")]
		[LocalizedRange(0.01, 1000000, "Please enter a packaging price between 0.01 and 1000000.", "Ingresá un precio de packaging entre 0,01 y 1000000.")]
		[DisplayName("Amount Total Payed")]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal Price { get; set; }
		[LocalizedRequired("Packaging quantity is required.", "La cantidad de packaging es obligatoria.")]
		[LocalizedRange(1, 100000, "Please enter a packaging quantity between 1 and 100000.", "Ingresá una cantidad de packaging entre 1 y 100000.")]
		public int Quantity { get; set; }
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
