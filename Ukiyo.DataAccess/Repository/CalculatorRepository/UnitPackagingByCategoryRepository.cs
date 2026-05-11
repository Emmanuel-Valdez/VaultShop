using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models;
using UkiyoDesigns.Models.CalculatorModels;


namespace UkiyoDesigns.DataAccess.Repository
{
	public class UnitPackagingByCategoryRepository : Repository<UnitPackagingByCategory>, IUnitPackagingByCategoryRepository
	{
		private ApplicationDbContext _db;
	
		public UnitPackagingByCategoryRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}

		public void Update(UnitPackagingByCategory obj)
		{
			_db.UnitsPackagingByCategory.Update(obj);
		}
	}
}

