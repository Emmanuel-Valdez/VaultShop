using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;

namespace VaultShop.DataAccess.Repository
{
	public class VariantOptionValueRepository : Repository<VariantOptionValue>, IVariantOptionValueRepository
	{
		public VariantOptionValueRepository(ApplicationDbContext db) : base(db)
		{
		}
	}
}
