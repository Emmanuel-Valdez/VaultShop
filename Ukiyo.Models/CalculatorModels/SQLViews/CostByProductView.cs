using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UkiyoDesigns.Models.CalculatorModels.SQLViews
{
	public class CostByProductView
	{
		public int ProductId { get; set; }
		[ForeignKey("ProductId")]
		public Product Product { get; set; } = null!;
		public string? CategoryName {  get; set; }
		[Column(TypeName = "decimal(18, 2)")]
		public decimal Packaging { get; set; } = 0.0m;
		[Column(TypeName = "decimal(18, 2)")]
		public decimal GarmentHardware { get; set; } = 0.0m;
		[Column(TypeName = "decimal(18, 2)")]
		public decimal Fabric { get; set; } = 0.0m;


		public int MaxExpectationMonthly { get; set; } = 0;
		[Column(TypeName = "decimal(18, 2)")]
		public decimal FixedCostAddedByCategory { get; set; } = 0.0m;

		[Column(TypeName = "decimal(18, 2)")]
		public decimal TotalCostByProduct { get; set; } = 0.0m;


	}
}
