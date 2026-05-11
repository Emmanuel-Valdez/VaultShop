using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UkiyoDesigns.DataAccess.Repository.CalculatorRepository.ReadOnlyRepository;
using UkiyoDesigns.Models;
using UkiyoDesigns.Models.CalculatorModels.SQLViews;

namespace UkiyoDesigns.DataAccess.Repository.IRepository.IReadOnlyRepositorys
{
    public interface IFinalPriceViewRepository : IReadOnlyRepository<FinalPriceView>
    {
    }
}
