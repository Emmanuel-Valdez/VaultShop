using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models.CalculatorModels;


namespace UkiyoDesigns.DataAccess.Repository
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
