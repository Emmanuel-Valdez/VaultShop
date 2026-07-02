using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UkiyoDesigns.Models;
using UkiyoDesigns.Models.CalculatorModels;

namespace UkiyoDesigns.DataAccess.Repository.IRepository
{
	public interface IUnitPackagingByCategoryRepository : IRepository<UnitPackagingByCategory>
	{
		void Update(UnitPackagingByCategory obj);
		
	}
}
