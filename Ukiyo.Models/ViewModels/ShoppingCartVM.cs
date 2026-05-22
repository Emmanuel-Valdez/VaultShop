using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UkiyoDesigns.Models.ViewModels
{
    public class ShoppingCartVM
    {
        [ValidateNever]
		public IEnumerable<ShoppingCart> ShoppingCartList {get;set;} = [];
		
		public OrderHeader OrderHeader { get;set;} = new();
    }
}
