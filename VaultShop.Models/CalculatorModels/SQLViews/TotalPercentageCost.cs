using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VaultShop.Models.CalculatorModels.SQLViews
{
    public class TotalPercentageCost
    {
		[Column(TypeName = "decimal(18, 2)")]
		[DisplayName("Total Percerntage Cost")]
		public decimal TotalPercentage { get; set; } = 0.0m;

    }
}