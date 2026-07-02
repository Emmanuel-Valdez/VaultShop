using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models.CalculatorModels;


namespace VaultShop.DataAccess.Repository
{
	public class PackagingByCategoryRepository : Repository<PackagingByCategory>, IPackagingByCategoryRepository
	{
		private ApplicationDbContext _db;
	
		public PackagingByCategoryRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}

		public void Update(PackagingByCategory obj)
		{
			
			var objFromDB = _db.PackagingsByCategory.FirstOrDefault(u => u.Id == obj.Id);
			if (objFromDB != null)
			{
				
				objFromDB.UnitPackagingByCategoryList = obj.UnitPackagingByCategoryList;
				objFromDB.CategoryId = obj.CategoryId;
			}
		}
	}
}
