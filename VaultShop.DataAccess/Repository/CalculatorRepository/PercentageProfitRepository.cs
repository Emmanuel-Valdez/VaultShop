using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models.CalculatorModels;


namespace UkiyoDesigns.DataAccess.Repository
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
