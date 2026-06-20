using UkiyoDesigns.Models.CalculatorModels;

namespace UkiyoDesigns.DataAccess.Repository.IRepository
{
	public interface IPercentageCostWholesaleRepository : IRepository<PercentageCostWholesale>
	{
		void Update(PercentageCostWholesale obj);
	}
}
