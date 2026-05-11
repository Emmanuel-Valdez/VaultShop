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
	public class PercentageProfit	
	{
		[Key]
		public int Id { get; set; }

		[Range(0, 99)]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal Wholesale { get; set; } = 0.0m;

		[Range(0, 99)]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal Retail { get; set; } = 0.0m;
	}
}

