using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models.CalculatorModels;


namespace UkiyoDesigns.DataAccess.Repository
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
