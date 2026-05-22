using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UkiyoDesigns.Models.CalculatorModels.SQLViews
{
	public class FinalPriceView
	{
		public int Id { get; set; }
		[ForeignKey("Id")]
		public Product Product { get; set; } = null!;
		public string? CategoryName {  get; set; }
		[Column(TypeName = "decimal(18, 2)")]
		public decimal AvgShippingCostByCategory { get; set; } = 0.0m;
		[Column(TypeName = "decimal(18, 2)")]
		public decimal TotalCost {  get; set; }= 0.0m;
		[Column(TypeName = "decimal(18, 2)")]
		public decimal RetailWithProfit { get; set; } = 0.0m;
		[Column(TypeName = "decimal(18, 2)")]
		public decimal RetailWithShipping { get; set; } = 0.0m;

		[Column(TypeName = "decimal(18, 2)")]
        public decimal FinalRetail { get; set; } = 0.0m;
		[Column(TypeName = "decimal(18, 2)")]
		public decimal WholesaleWithProfit { get; set; } = 0.0m;
		[Column(TypeName = "decimal(18, 2)")]
		public decimal ActualListPrice { get; set; } = 0.0m;
		[Column(TypeName = "decimal(18, 2)")]
		public decimal ActualRetailPrice { get; set; } = 0.0m;
		[Column(TypeName = "decimal(18, 2)")]
		public decimal ActualWholesalePrice { get; set; } = 0.0m;
	}
}
