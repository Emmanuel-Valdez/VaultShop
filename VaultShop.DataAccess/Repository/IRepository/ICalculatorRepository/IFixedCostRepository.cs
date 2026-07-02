using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VaultShop.Models;
using VaultShop.Models.CalculatorModels;

namespace VaultShop.DataAccess.Repository.IRepository
{
	public interface IFixedCostRepository : IRepository<FixedCost>
	{
		void Update(FixedCost obj);
		
	}
}
