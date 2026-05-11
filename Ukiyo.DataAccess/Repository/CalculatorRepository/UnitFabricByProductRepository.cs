using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models.CalculatorModels;


namespace UkiyoDesigns.DataAccess.Repository
{
	public class UnitFabricByProductRepository : Repository<UnitFabricByProduct>, IUnitFabricByProductRepository
	{
		private ApplicationDbContext _db;
	
		public UnitFabricByProductRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}

		public void Update(UnitFabricByProduct obj)
		{
			_db.UnitsFabricByProduct.Update(obj);
		}
	}
}
