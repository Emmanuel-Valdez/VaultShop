using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UkiyoDesigns.Models;
using UkiyoDesigns.Models.CalculatorModels;

namespace UkiyoDesigns.DataAccess.Repository.IRepository
{
	public interface IPackagingByCategoryRepository : IRepository<PackagingByCategory>
	{
		void Update(PackagingByCategory obj);
		
	}
}
