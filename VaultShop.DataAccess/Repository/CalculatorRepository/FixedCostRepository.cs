using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models.CalculatorModels;


namespace VaultShop.DataAccess.Repository
{
	public class FixedCostRepository : Repository<FixedCost>, IFixedCostRepository
	{
		private ApplicationDbContext _db;
	
		public FixedCostRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}
		public void Update(FixedCost obj)
		{
			_db.FixedCosts.Update(obj);
		}
	}
}
