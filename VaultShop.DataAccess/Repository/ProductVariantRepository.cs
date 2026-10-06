using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;

namespace VaultShop.DataAccess.Repository
{
	public class ProductVariantRepository : Repository<ProductVariant>, IProductVariantRepository
	{
		public ProductVariantRepository(ApplicationDbContext db) : base(db)
		{
		}
	}
}
