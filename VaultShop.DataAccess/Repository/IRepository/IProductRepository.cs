using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VaultShop.Models;

namespace VaultShop.DataAccess.Repository.IRepository
{
	public interface IProductRepository: IRepository<Product>
	{
		void Update(Product obj);
		void UpdateRange(IEnumerable<Product> obj);
		// product-variants-hardening: conditional relative decrement — the real concurrency guard.
		// Returns affected rows (1 when the pool covered total, 0 otherwise). Never goes negative.
		int DecrementStockIfSufficient(int productId, int total);
	}
}
