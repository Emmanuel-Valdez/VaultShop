using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;

namespace VaultShop.DataAccess.Repository
{
	public class BannerRepository : Repository<Banner>, IBannerRepository
	{
		private readonly ApplicationDbContext _db;

		public BannerRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}

		public void Update(Banner obj)
		{
			_db.Banners.Update(obj);
		}
	}
}
