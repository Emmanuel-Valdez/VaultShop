using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;

namespace VaultShop.DataAccess.Repository
{
	public class ProductVariantValueRepository : Repository<ProductVariantValue>, IProductVariantValueRepository
	{
		public ProductVariantValueRepository(ApplicationDbContext db) : base(db)
		{
		}
	}
}
