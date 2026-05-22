using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UkiyoDesigns.Models.CalculatorModels
{
	public class UnitFabricByProduct
	{
        public int Id { get; set; }
		[Display(Name = "Description (Optional)")]
		public string? Description { get; set; }
		public int ProductId { get; set; }
		[ForeignKey("ProductId")]
		[ValidateNever]
		public Product Product { get; set; } = null!;
		public int FabricId { get; set; }
		[ForeignKey("FabricId")]
		[ValidateNever]
		public Fabric Fabric { get; set; } = null!;
		[Required]
		[Range(0.001,10000)]
		[Display(Name = "Products Made Per Meter")]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal Quantity { get; set; }
		[Column(TypeName = "decimal(18, 2)")]
		public decimal UnitTotal { get; private set; }
	}
}
