using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UkiyoDesigns.Models.CalculatorModels
{
	public class PercentageCost
	{
		[Key]
		public int Id { get; set; }
		[Required]
		public string Name { get; set; } = string.Empty;
		[Required]
		[Range(0, 100)]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal Percentage { get; set; }
        public string? Description { get; set; }
	}
}
