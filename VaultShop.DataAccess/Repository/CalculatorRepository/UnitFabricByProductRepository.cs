using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models.CalculatorModels;


namespace VaultShop.DataAccess.Repository
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
