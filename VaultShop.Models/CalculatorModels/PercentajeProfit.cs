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
	public class PercentageProfit	
	{
		[Key]
		public int Id { get; set; }

		[LocalizedRange(0, 99, "Please enter a wholesale profit between 0 and 99.", "Ingresá una ganancia mayorista entre 0 y 99.")]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal Wholesale { get; set; } = 0.0m;

		[LocalizedRange(0, 99, "Please enter a retail profit between 0 and 99.", "Ingresá una ganancia minorista entre 0 y 99.")]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal Retail { get; set; } = 0.0m;
	}
}

