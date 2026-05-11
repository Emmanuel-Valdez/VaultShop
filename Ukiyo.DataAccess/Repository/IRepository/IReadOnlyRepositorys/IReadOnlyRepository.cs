using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace UkiyoDesigns.DataAccess.Repository.IRepository.IReadOnlyRepositorys
{
    public interface IReadOnlyRepository<T> where T : class
    {
        //T category
        IEnumerable<T> GetAll(Expression<Func<T, bool>>? filter = null, string? includeProperties = null, bool tracked = false);
        T Get(Expression<Func<T, bool>> filter, string? includeProperties = null, bool tracked = false);

    }
}
