using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models;

namespace UkiyoDesigns.DataAccess.Repository
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
