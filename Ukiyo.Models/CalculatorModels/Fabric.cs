using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UkiyoDesigns.Models.CalculatorModels
{
	public class Fabric
	{
		[Key]
		public int Id { get; set; }
		[Required]
		public string Name { get; set; }
		[Required]
		[Range(0.01, 1000000)]
		[DisplayName("Amount Total Payed")]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal Price { get; set; }
		[Required]
		[Range(0.01, 100000)]
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