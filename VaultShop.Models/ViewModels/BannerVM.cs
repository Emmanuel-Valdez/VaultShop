using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace VaultShop.Models.ViewModels
{
	public class BannerVM
	{
		public Banner Banner { get; set; } = new();
		[ValidateNever]
		public IEnumerable<SelectListItem> LayoutList { get; set; } = [];
		[ValidateNever]
		public IEnumerable<SelectListItem> TextColorList { get; set; } = [];
		[ValidateNever]
		public IEnumerable<SelectListItem> TargetKindList { get; set; } = [];
		[ValidateNever]
		public IEnumerable<SelectListItem> TargetOptions { get; set; } = [];
	}
}
