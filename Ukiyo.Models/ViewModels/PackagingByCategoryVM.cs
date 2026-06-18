using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UkiyoDesigns.Models.CalculatorModels;

namespace UkiyoDesigns.Models.ViewModels
{
	public class PackagingByCategoryVM
	{
		public PackagingByCategory PackagingByCategory { get; set; } = new();
		[ValidateNever]
		public IEnumerable<SelectListItem> PackagingList { get; set; } = [];
		

		public UnitPackagingByCategory UnitPackagingByCategory { get; set; } = new();
		public decimal CalculatedTotalByCategory { get; set; }
	}
}

