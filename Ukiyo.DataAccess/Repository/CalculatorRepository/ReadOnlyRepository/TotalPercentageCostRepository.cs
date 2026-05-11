using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository.IRepository.IReadOnlyRepositorys;
using UkiyoDesigns.Models.CalculatorModels.SQLViews;


namespace UkiyoDesigns.DataAccess.Repository.CalculatorRepository.ReadOnlyRepository
{
    public class TotalPercentageCostRepository : ReadOnlyRepository<TotalPercentageCost>, ITotalPercentageCostRepository
    {
        private ApplicationDbContext _db;

        public TotalPercentageCostRepository(ApplicationDbContext db) : base(db)
        {
            _db = db;
        }

    }
}
