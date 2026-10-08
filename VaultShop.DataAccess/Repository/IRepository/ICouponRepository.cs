using VaultShop.Models;

namespace VaultShop.DataAccess.Repository.IRepository
{
	public interface ICouponRepository : IRepository<Coupon>
	{
		void Update(Coupon obj);
		int TryIncrementUses(int couponId);
	}
}
