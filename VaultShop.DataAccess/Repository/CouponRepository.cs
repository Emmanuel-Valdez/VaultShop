using VaultShop.DataAccess.Data;
using Microsoft.EntityFrameworkCore;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;

namespace VaultShop.DataAccess.Repository
{
	public class CouponRepository : Repository<Coupon>, ICouponRepository
	{
		private readonly ApplicationDbContext _db;

		public CouponRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}

		public void Update(Coupon obj)
		{
			_db.Coupons.Update(obj);
		}

		public int TryIncrementUses(int couponId)
			=> _db.Coupons
				.Where(c => c.Id == couponId && (!c.MaxUses.HasValue || c.UsesCount < c.MaxUses.Value))
				.ExecuteUpdate(s => s.SetProperty(c => c.UsesCount, c => c.UsesCount + 1));
	}
}
