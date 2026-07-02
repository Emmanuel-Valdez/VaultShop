using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Models.CalculatorModels;


namespace VaultShop.DataAccess.Repository
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

