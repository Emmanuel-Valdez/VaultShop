using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UkiyoDesigns.Models.Validation;

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
		[LocalizedRequired("Quantity is required.", "La cantidad es obligatoria.")]
		[LocalizedRange(0.001, 10000, "Please enter a quantity between 0.001 and 10000.", "Ingresá una cantidad entre 0,001 y 10000.")]
		[Display(Name = "Products Made Per Meter")]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal Quantity { get; set; }
		[Column(TypeName = "decimal(18, 2)")]
		public decimal UnitTotal { get; private set; }
	}
}
