using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UkiyoDesigns.Models.CalculatorModels.SQLViews
{
    public class FixedCostMonthly
    {
		[Column(TypeName = "decimal(18, 2)")]
		public decimal TotalFixedCostMonthly { get; set; }

	}
}