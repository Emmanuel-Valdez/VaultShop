using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VaultShop.Models
{
	public class ProductVariant
	{
		[Key]
		public int Id { get; set; }

		public int ProductId { get; set; }
		[ForeignKey("ProductId")]
		[ValidateNever]
		public Product Product { get; set; } = null!;

		public bool IsAvailable { get; set; } = true;

		[ValidateNever]
		public List<ProductVariantValue> Values { get; set; } = new();
	}
}
