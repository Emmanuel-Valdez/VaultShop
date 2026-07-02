using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models;

namespace UkiyoDesigns.DataAccess.Repository
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
