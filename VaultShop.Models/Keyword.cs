using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using VaultShop.Models.Validation;

namespace VaultShop.Models
{
	public class Keyword
	{
		[Key]
		public int Id { get; set; }

		[LocalizedRequired("Keyword name is required.", "El nombre de la colección es obligatorio.")]
		[MaxLength(100)]
		public string Name { get; set; } = string.Empty;

		[MaxLength(120)]
		public string Slug { get; set; } = string.Empty;

		public bool IsDeleted { get; set; } = false;

		// Reserved placeholder for a future Category→Keyword relationship. No FK, no navigation, no behavior in v1.
		public int? CategoryId { get; set; }

		[ValidateNever]
		public List<KeywordImage> Images { get; set; } = new();

		[ValidateNever]
		public List<ProductKeyword> ProductKeywords { get; set; } = new();
	}
}