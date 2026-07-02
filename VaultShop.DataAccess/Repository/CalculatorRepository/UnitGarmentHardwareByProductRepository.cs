using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models.CalculatorModels;


namespace VaultShop.DataAccess.Repository
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
