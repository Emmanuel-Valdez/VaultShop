using VaultShop.Models.CalculatorModels;

namespace VaultShop.DataAccess.Repository.IRepository
{
	public interface IPercentageCostWholesaleRepository : IRepository<PercentageCostWholesale>
	{
		void Update(PercentageCostWholesale obj);
	}
}
