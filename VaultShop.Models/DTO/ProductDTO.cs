using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UkiyoDesigns.Models.DTO
{
	public class ProductDTO
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public decimal TotalByProduct { get; set; }
	}
}
