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
	public class UnitPackagingByCategory
	{
        public int Id { get; set; }
		[Display(Name = "Description (Optional)")]
		public string? Description { get; set; }
		public int CategoryId { get; set; }
		[ForeignKey("CategoryId")]
		[ValidateNever]
		public Category Category { get; set; } = null!;
		public int PackagingId { get; set; }
		[ForeignKey("PackagingId")]
		[ValidateNever]
		public Packaging Packaging { get; set; } = null!;
		[Required]
		[Range(0.001,10000)]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal Quantity { get; set; }
		[Column(TypeName = "decimal(18, 2)")]
		public decimal UnitTotal { get; private set; }
	}
}
