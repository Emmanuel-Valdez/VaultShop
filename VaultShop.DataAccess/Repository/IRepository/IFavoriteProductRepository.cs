using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UkiyoDesigns.Models;

namespace UkiyoDesigns.DataAccess.Repository.IRepository
{
	public interface IFavoriteProductRepository : IRepository<FavoriteProduct>
	{
		void Update(FavoriteProduct obj);
		
	}
}
