using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models.CalculatorModels;


namespace UkiyoDesigns.DataAccess.Repository
{
	public class UnitGarmentHardwareByProductRepository : Repository<UnitGarmentHardwareByProduct>, IUnitGarmentHardwareByProductRepository
	{
		private ApplicationDbContext _db;
	
		public UnitGarmentHardwareByProductRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}

		public void Update(UnitGarmentHardwareByProduct obj)
		{
			_db.UnitsGarmentHardwareByProduct.Update(obj);
		}
	}
}
