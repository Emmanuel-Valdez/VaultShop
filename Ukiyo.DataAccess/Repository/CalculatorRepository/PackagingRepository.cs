using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models.CalculatorModels;


namespace UkiyoDesigns.DataAccess.Repository
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
