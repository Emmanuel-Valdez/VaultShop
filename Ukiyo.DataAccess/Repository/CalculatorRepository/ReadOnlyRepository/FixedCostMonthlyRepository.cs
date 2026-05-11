using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Linq;
using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository.IRepository.IReadOnlyRepositorys;
using UkiyoDesigns.Models.CalculatorModels.SQLViews;


namespace UkiyoDesigns.DataAccess.Repository.CalculatorRepository.ReadOnlyRepository
{
    public class FixedCostMonthlyRepository : ReadOnlyRepository<FixedCostMonthly>, IFixedCostMonthlyRepository
    {
        private ApplicationDbContext _db;

        public FixedCostMonthlyRepository(ApplicationDbContext db) : base(db)
        {
            _db = db;
        }

	}
}
