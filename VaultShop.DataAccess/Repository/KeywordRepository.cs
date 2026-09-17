using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;

namespace VaultShop.DataAccess.Repository
{
	public class KeywordRepository : Repository<Keyword>, IKeywordRepository
	{
		private readonly ApplicationDbContext _db;

		public KeywordRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}

		public void Update(Keyword obj)
		{
			_db.Keywords.Update(obj);
		}
	}
}