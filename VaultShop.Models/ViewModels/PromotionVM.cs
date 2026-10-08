using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace VaultShop.Models.ViewModels
{
	public class PromotionVM
	{
		public Promotion Promotion { get; set; } = new();
		[ValidateNever]
		public IEnumerable<SelectListItem> ProductList { get; set; } = [];
		[ValidateNever]
		public IEnumerable<SelectListItem> CategoryList { get; set; } = [];
		[ValidateNever]
		public IEnumerable<SelectListItem> KeywordList { get; set; } = [];
		[ValidateNever]
		public IEnumerable<SelectListItem> PaymentMethodList { get; set; } = [];
	}
}
