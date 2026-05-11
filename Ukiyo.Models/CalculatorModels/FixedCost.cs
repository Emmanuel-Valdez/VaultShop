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
	public class FixedCost
	{
		[Key]
		public int Id { get; set; }
		[Required]
		public string Name { get; set; }
		[Required]
		[DisplayName("Approximate monthly fixed cost")]
		[Range(0, 10000000)]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal Cost { get; set; }
        public string? Description { get; set; }
	}
}