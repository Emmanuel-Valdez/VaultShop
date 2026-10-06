using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace VaultShop.Models
{
	public class VariantOptionType
	{
		[Key]
		public int Id { get; set; }

		[Required]
		[MaxLength(100)]
		public string Name { get; set; } = string.Empty;

		[ValidateNever]
		public List<VariantOptionValue> Values { get; set; } = new();
	}
}
