using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace VaultShop.Models.CalculatorModels.SQLViews
{
	public class TotalPercentageCostWholesale
	{
		[Column(TypeName = "decimal(18, 2)")]
		[DisplayName("Total Wholesale Percentage Cost")]
		public decimal TotalPercentage { get; set; } = 0.0m;
	}
}
