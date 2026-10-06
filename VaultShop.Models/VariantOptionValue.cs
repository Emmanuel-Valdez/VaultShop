using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VaultShop.Models
{
	public class VariantOptionValue
	{
		[Key]
		public int Id { get; set; }

		public int ProductId { get; set; }
		[ForeignKey("ProductId")]
		[ValidateNever]
		public Product Product { get; set; } = null!;

		public int VariantOptionTypeId { get; set; }
		[ForeignKey("VariantOptionTypeId")]
		[ValidateNever]
		public VariantOptionType VariantOptionType { get; set; } = null!;

		[Required]
		[MaxLength(100)]
		public string Value { get; set; } = string.Empty;

		public int SortOrder { get; set; } = 0;

		[ValidateNever]
		public List<ProductVariantValue> VariantValues { get; set; } = new();
	}
}
