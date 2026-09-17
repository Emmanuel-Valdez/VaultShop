using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;

namespace VaultShop.DataAccess.Repository
{
	public class KeywordImageRepository : Repository<KeywordImage>, IKeywordImageRepository
	{
		private readonly ApplicationDbContext _db;

		public KeywordImageRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}

		public void Update(KeywordImage obj)
		{
			_db.KeywordImages.Update(obj);
		}
	}
}