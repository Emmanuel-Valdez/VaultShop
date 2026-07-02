using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models.CalculatorModels;


namespace VaultShop.DataAccess.Repository
{
	public class GarmentHardwareRepository : Repository<GarmentHardware>, IGarmentHardwareRepository
	{
		private ApplicationDbContext _db;
	
		public GarmentHardwareRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}

		public void Update(GarmentHardware obj)
		{
			_db.GarmentHardwares.Update(obj);
		}
	}
}
