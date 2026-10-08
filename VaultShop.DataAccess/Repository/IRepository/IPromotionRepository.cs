using VaultShop.Models;

namespace VaultShop.DataAccess.Repository.IRepository
{
	public interface IPromotionRepository : IRepository<Promotion>
	{
		void Update(Promotion obj);
	}
}
