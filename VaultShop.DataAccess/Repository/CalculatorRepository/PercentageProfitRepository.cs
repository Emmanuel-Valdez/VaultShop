using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models.CalculatorModels;


namespace VaultShop.DataAccess.Repository
{
	public class PercentageProfitRepository : Repository<PercentageProfit>, IPercentageProfitRepository
	{
		private ApplicationDbContext _db;
	
		public PercentageProfitRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}

		public void Update(PercentageProfit obj)
		{
			_db.PercentageProfits.Update(obj);
		}
	}
}
