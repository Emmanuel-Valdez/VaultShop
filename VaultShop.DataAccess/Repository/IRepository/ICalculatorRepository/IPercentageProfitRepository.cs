using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VaultShop.Models;
using VaultShop.Models.CalculatorModels;

namespace VaultShop.DataAccess.Repository.IRepository
{
	public interface IPercentageProfitRepository : IRepository<PercentageProfit>
	{
		void Update(PercentageProfit obj);
		
	}
}
