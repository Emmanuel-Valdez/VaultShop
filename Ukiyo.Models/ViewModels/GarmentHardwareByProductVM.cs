using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UkiyoDesigns.Models.CalculatorModels;

namespace UkiyoDesigns.Models.ViewModels
{
	public class GarmentHardwareByProductVM
	{
		public GarmentHardwareByProduct GarmentHardwareByProduct { get; set; }
		[ValidateNever]
		public IEnumerable<SelectListItem> GarmentHardwareList { get; set; }


		public UnitGarmentHardwareByProduct UnitGarmentHardwareByProduct { get; set; }
	}
}
