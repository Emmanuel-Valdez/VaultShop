using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository.IRepository.IReadOnlyRepositorys;
using UkiyoDesigns.Models.CalculatorModels.SQLViews;


namespace UkiyoDesigns.DataAccess.Repository.CalculatorRepository.ReadOnlyRepository
{
    public class CostByProductViewRepository : ReadOnlyRepository<CostByProductView>, ICostByProductViewRepository
	{
        private ApplicationDbContext _db;

        public CostByProductViewRepository(ApplicationDbContext db) : base(db)
        {
            _db = db;
        }

    }
}
