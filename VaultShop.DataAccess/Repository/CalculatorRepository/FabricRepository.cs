using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models.CalculatorModels;


namespace UkiyoDesigns.DataAccess.Repository
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
