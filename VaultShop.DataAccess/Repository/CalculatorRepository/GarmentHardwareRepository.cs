using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models.CalculatorModels;


namespace UkiyoDesigns.DataAccess.Repository
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
