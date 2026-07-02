using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models.CalculatorModels;

namespace VaultShop.DataAccess.Repository
{
	public class PercentageCostWholesaleRepository : Repository<PercentageCostWholesale>, IPercentageCostWholesaleRepository
	{
		private readonly ApplicationDbContext _db;

		public PercentageCostWholesaleRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}

		public void Update(PercentageCostWholesale obj)
		{
			_db.PercentageCostsWholesale.Update(obj);
		}
	}
}
