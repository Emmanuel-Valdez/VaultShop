using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VaultShop.Models.CalculatorModels
{
	public class PackagingByCategory
	{
		public int Id { get; set; }
		public int CategoryId { get; set; }
		[ForeignKey("CategoryId")]
		[ValidateNever]
		public Category Category { get; set; } = null!;
		[ValidateNever]
		public List<UnitPackagingByCategory> UnitPackagingByCategoryList { get; set; } = new();
	}
}
