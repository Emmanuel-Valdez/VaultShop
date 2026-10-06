using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations.Schema;

namespace VaultShop.Models
{
	public class ProductVariantValue
	{
		public int VariantId { get; set; }
		[ForeignKey("VariantId")]
		[ValidateNever]
		public ProductVariant Variant { get; set; } = null!;

		public int ValueId { get; set; }
		[ForeignKey("ValueId")]
		[ValidateNever]
		public VariantOptionValue Value { get; set; } = null!;
	}
}
