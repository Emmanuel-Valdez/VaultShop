using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VaultShop.DataAccess.DbInitializer
{
	public interface IDbInitializer
	{
		void Initialize();
	}

	public interface IDemoDataSeeder
	{
		bool HasProducts();
		bool HasOrders();
		void SeedDemoCatalog();
		void SeedDemoUsers();
		void SeedDemoShoppingActivity();
		void SeedDemoOrders();
	}
}
