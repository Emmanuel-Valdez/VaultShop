using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VaultShop.Models.CalculatorModels;
using VaultShop.Models.CalculatorModels.SQLViews;

namespace VaultShop.Models.ViewModels
{
	public class FinalPriceVM
	{
		[ValidateNever]
		public TotalPercentageCost TotalPercentageCost { get; set; } = new();
		[ValidateNever]
		public TotalPercentageCostWholesale TotalPercentageCostWholesale { get; set; } = new();
		public PercentageProfit PercentageProfit { get; set; } = new();
		public int CountOutdated { get; set; }

	}
}
