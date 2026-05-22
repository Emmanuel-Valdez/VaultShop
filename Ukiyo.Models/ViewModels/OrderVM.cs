using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UkiyoDesigns.Models.ViewModels
{
	public class OrderVM
	{
		public OrderHeader OrderHeader { get; set; } = new();
		[ValidateNever]
		public IEnumerable<OrderDetail> OrderDetail { get; set; } = [];
    }
}
