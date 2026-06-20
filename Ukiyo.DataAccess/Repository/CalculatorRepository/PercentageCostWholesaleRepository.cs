using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models.CalculatorModels;

namespace UkiyoDesigns.DataAccess.Repository
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
