using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations.Schema;

namespace VaultShop.Models
{
	public class ProductKeyword
	{
		public int ProductId { get; set; }
		[ForeignKey("ProductId")]
		[ValidateNever]
		public Product Product { get; set; } = null!;

		public int KeywordId { get; set; }
		[ForeignKey("KeywordId")]
		[ValidateNever]
		public Keyword Keyword { get; set; } = null!;
	}
}