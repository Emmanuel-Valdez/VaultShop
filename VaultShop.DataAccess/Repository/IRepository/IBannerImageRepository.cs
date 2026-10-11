using VaultShop.Models;

namespace VaultShop.DataAccess.Repository.IRepository
{
	public interface IBannerImageRepository : IRepository<BannerImage>
	{
		void Update(BannerImage obj);
	}
}
