using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models.CalculatorModels;


namespace VaultShop.DataAccess.Repository
{
	public class PercentageCostRepository : Repository<PercentageCost>, IPercentageCostRepository
	{
		private ApplicationDbContext _db;
	
		public PercentageCostRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}
		public void Update(PercentageCost obj)
		{
			_db.PercentageCosts.Update(obj);
		}
	}
}
