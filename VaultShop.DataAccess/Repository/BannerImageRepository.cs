using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;

namespace VaultShop.DataAccess.Repository
{
	public class BannerImageRepository : Repository<BannerImage>, IBannerImageRepository
	{
		private readonly ApplicationDbContext _db;

		public BannerImageRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}

		public void Update(BannerImage obj)
		{
			_db.BannerImages.Update(obj);
		}
	}
}
