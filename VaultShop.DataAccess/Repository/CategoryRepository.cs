using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;

namespace VaultShop.DataAccess.Repository
{
	public class CategoryRepository : Repository<Category>, ICategoryRepository
	{
		private ApplicationDbContext _db;
	
		public CategoryRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}

		public void Update(Category obj)
		{
			_db.Categories.Update(obj);
		}
	}
}
