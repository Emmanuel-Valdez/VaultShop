using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models.CalculatorModels;


namespace VaultShop.DataAccess.Repository
{
	public class PackagingRepository : Repository<Packaging>, IPackagingRepository
	{
		private ApplicationDbContext _db;
	
		public PackagingRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}

		public void Update(Packaging obj)
		{
			_db.Packagings.Update(obj);
		}
	}
}
