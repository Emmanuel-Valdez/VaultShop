using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UkiyoDesigns.Models.DTO
{
	public class CategoryDTO
	{
		public int Id { get; set; }
		public string Name { get; set; }
		public decimal TotalPackagingByCategory { get; set; }
	}
}
