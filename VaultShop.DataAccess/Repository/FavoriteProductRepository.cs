using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;

namespace VaultShop.DataAccess.Repository
{
	public class FavoriteProductRepository : Repository<FavoriteProduct>, IFavoriteProductRepository
	{
		private ApplicationDbContext _db;
	
		public FavoriteProductRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}

		public void Update(FavoriteProduct obj)
		{
			_db.FavoriteProducts.Update(obj);
		}
	}
}
