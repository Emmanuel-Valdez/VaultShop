using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;

namespace VaultShop.DataAccess.Repository
{
	public class ProductImageRepository : Repository<ProductImage>, IProductImageRepository
	{
		private ApplicationDbContext _db;
	
		public ProductImageRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}

		public void Update(ProductImage obj)
		{
			_db.ProductImages.Update(obj);
		}
	}
}
