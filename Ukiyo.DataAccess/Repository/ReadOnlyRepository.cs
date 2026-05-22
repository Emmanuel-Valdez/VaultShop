
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository.IRepository.IReadOnlyRepositorys;

namespace UkiyoDesigns.DataAccess.Repository
{
    public class ReadOnlyRepository<T> : IReadOnlyRepository<T> where T : class
	{
		private readonly ApplicationDbContext _db;
		internal DbSet<T> dbSet;

        public ReadOnlyRepository(ApplicationDbContext db)
        {
			_db = db;
			this.dbSet = _db.Set<T>();
			//_db.Products.Include(u => u.Category).Include(u=>u.Telas).Include ... 
			//puede contener varios datos de distintas tablas
			//_db.Products.Include(u => u.Category);
        }

		public T? Get(Expression<Func<T, bool>> filter, string? includeProperties = null, bool tracked =false)
		{
			if (tracked)
			{
                IQueryable<T> query = dbSet;
                query = query.Where(filter);
                if (!string.IsNullOrEmpty(includeProperties))
                {
                    foreach (var includeProp in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        query = query.Include(includeProp);
                    }
                }
                return query.FirstOrDefault();
            }
			else
			{
                IQueryable<T> query = dbSet.AsNoTracking();
                query = query.Where(filter);
                if (!string.IsNullOrEmpty(includeProperties))
                {
                    foreach (var includeProp in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        query = query.Include(includeProp);
                    }
                }
                return query.FirstOrDefault();
            }

			
		}

		//public IEnumerable<T> GetAll(Expression<Func<T, bool>>? filter, string? includeProperties = null, bool tracked = false)
		//{
		//	if (tracked)
		//	{
		//		IQueryable<T> query = dbSet;
		//		if (filter != null)
		//			query = query.Where(filter);

		//		if (!string.IsNullOrEmpty(includeProperties))
		//		{
		//			foreach (var includeProp in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
		//			{
		//				query = query.Include(includeProp);
		//			}
		//		}
		//		return query.ToList();
		//	}
		//	else
		//	{
		//		IQueryable<T> query = dbSet.AsNoTracking();
		//		if (filter != null)
		//			query = query.Where(filter);

		//		if (!string.IsNullOrEmpty(includeProperties))
		//		{
		//			foreach (var includeProp in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
		//			{
		//				query = query.Include(includeProp);
		//			}
		//		}
		//		return query.ToList();
		//	}
		//}
		public IEnumerable<T> GetAll(Expression<Func<T, bool>>? filter, string? includeProperties = null, bool tracked = false)
		{
			IQueryable<T> query;

			if (tracked)
			{
				query = dbSet;
			}
			else
			{
				query = dbSet.AsNoTracking();
			}

			if (filter != null)
			{
				query = query.Where(filter);
			}

			if (!string.IsNullOrEmpty(includeProperties))
			{
				foreach (var includeProp in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
				{
					query = query.Include(includeProp);
				}
			}

			return query.ToList(); ;
		}


	}
}
