using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository.IRepository.IReadOnlyRepositorys;
using UkiyoDesigns.Models.CalculatorModels;

namespace UkiyoDesigns.DataAccess.Repository.IRepository
{
    public interface IUnitOfWork
	{

		ICategoryRepository Category { get; }
		IProductRepository Product { get; }
		ICompanyRepository Company { get; }
		IShoppingCartRepository ShoppingCart { get; }
		IOrderHeaderRepository OrderHeader { get; }
		IOrderDetailRepository OrderDetail { get; }
		IApplicationUserRepository ApplicationUser { get; }
		IProductImageRepository ProductImage { get; }

		//Calculator Interfaces
		IPackagingRepository Packaging { get; }
		IPackagingByCategoryRepository PackagingByCategory { get; }
		IUnitPackagingByCategoryRepository UnitPackagingByCategory { get; }

		IFabricRepository Fabric { get; }
		IFabricByProductRepository FabricByProduct { get; }
		IUnitFabricByProductRepository UnitFabricByProduct { get; }

		IGarmentHardwareRepository GarmentHardware {  get; }
		IGarmentHardwareByProductRepository GarmentHardwareByProduct { get; }
		IUnitGarmentHardwareByProductRepository UnitGarmentHardwareByProduct { get; }

		IFixedCostRepository FixedCost { get; }
		IFixedCostMonthlyRepository FixedCostMonthly { get; }

		IPercentageCostRepository PercentageCost { get; }
		ITotalPercentageCostRepository TotalPercentageCost { get; }

		ICostByProductViewRepository CostByProductView { get; }

		IPercentageProfitRepository PercentageProfit { get; }

		IFinalPriceViewRepository FinalPriceView{  get; }
		IFavoriteProductRepository FavoriteProduct { get; }


		void Save();
		void ExecuteInTransaction(Action operation);
		public void UpdateEntityValues<TEntity>(TEntity existingEntity, TEntity newValues) where TEntity : class;
	} 
}
