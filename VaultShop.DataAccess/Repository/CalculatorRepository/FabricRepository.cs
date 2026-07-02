using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models.CalculatorModels;


namespace VaultShop.DataAccess.Repository
{
	public class FabricRepository : Repository<Fabric>, IFabricRepository
	{
		private ApplicationDbContext _db;
	
		public FabricRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}

		public void Update(Fabric obj)
		{
			_db.Fabrics.Update(obj);
		}
	}
}
