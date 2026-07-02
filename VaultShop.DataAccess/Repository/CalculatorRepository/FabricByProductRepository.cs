using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models.CalculatorModels;


namespace UkiyoDesigns.DataAccess.Repository
{
	public class FabricByProductRepository : Repository<FabricByProduct>, IFabricByProductRepository
	{
		private ApplicationDbContext _db;
	
		public FabricByProductRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}

		public void Update(FabricByProduct obj)
		{
			var objFromDB = _db.FabricsByProduct.FirstOrDefault(u => u.Id == obj.Id);
			if (objFromDB != null)
			{
				objFromDB.UnitFabricByProductList = obj.UnitFabricByProductList;
				objFromDB.ProductId = obj.ProductId;
			}
		}
	}
}
