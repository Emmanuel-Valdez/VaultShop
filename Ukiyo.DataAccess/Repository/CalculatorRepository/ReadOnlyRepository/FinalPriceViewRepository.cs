using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository.IRepository.IReadOnlyRepositorys;
using UkiyoDesigns.Models.CalculatorModels.SQLViews;


namespace UkiyoDesigns.DataAccess.Repository.CalculatorRepository.ReadOnlyRepository
{
    public class FinalPriceViewRepository : ReadOnlyRepository<FinalPriceView>, IFinalPriceViewRepository
	{
        private ApplicationDbContext _db;

        public FinalPriceViewRepository(ApplicationDbContext db) : base(db)
        {
            _db = db;
        }

    }
}
