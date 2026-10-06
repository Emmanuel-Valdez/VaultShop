using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;

namespace VaultShop.DataAccess.Repository
{
	public class VariantOptionTypeRepository : Repository<VariantOptionType>, IVariantOptionTypeRepository
	{
		public VariantOptionTypeRepository(ApplicationDbContext db) : base(db)
		{
		}
	}
}
