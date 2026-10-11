using VaultShop.Models;

namespace VaultShop.DataAccess.Repository.IRepository
{
	public interface IBannerRepository : IRepository<Banner>
	{
		void Update(Banner obj);
	}
}
