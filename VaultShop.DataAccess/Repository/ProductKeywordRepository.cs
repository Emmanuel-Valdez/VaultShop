using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;

namespace VaultShop.DataAccess.Repository
{
	public class ProductKeywordRepository : Repository<ProductKeyword>, IProductKeywordRepository
	{
		public ProductKeywordRepository(ApplicationDbContext db) : base(db)
		{
		}
	}
}