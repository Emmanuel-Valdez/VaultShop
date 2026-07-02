using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models.CalculatorModels;


namespace VaultShop.DataAccess.Repository
{
	public class GarmentHardwareByProductRepository : Repository<GarmentHardwareByProduct>, IGarmentHardwareByProductRepository
	{
		private ApplicationDbContext _db;
	
		public GarmentHardwareByProductRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}

		public void Update(GarmentHardwareByProduct obj)
		{
			var objFromDB = _db.GarmentHardwaresByProduct.FirstOrDefault(u => u.Id == obj.Id);
			if (objFromDB != null)
			{
				objFromDB.UnitGarmentHardwareByProductList = obj.UnitGarmentHardwareByProductList;
				objFromDB.ProductId = obj.ProductId;
			}
		}
	}
}
