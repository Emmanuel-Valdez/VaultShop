using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models.CalculatorModels;


namespace UkiyoDesigns.DataAccess.Repository
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
