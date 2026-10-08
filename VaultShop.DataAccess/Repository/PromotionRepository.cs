using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;

namespace VaultShop.DataAccess.Repository
{
	public class PromotionRepository : Repository<Promotion>, IPromotionRepository
	{
		private readonly ApplicationDbContext _db;

		public PromotionRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}

		public void Update(Promotion obj)
		{
			_db.Promotions.Update(obj);
		}
	}
}
