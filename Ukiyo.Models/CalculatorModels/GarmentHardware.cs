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
	public class GarmentHardware
	{
		[Key]
		public int Id { get; set; }
		[Required]
		public string Name { get; set; }
		[Required]
		[DisplayName("Amount Total Payed")]
		[Range(0.01, 1000000)]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal Price { get; set; }
		[Required]
		[Range(0.01, 100000)]
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