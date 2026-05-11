using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models.CalculatorModels;


namespace UkiyoDesigns.DataAccess.Repository
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
