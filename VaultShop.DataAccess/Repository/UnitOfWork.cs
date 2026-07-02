using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;

namespace VaultShop.DataAccess.Repository
{
    public class UnitOfWork : IUnitOfWork
    {
        private ApplicationDbContext _db;
        public ICategoryRepository Category { get; private set; }
        public IProductRepository Product { get; private set; }
        public ICompanyRepository Company { get; private set; }
		public IShoppingCartRepository ShoppingCart { get; private set; }
        public IApplicationUserRepository ApplicationUser { get; private set; }
		public IOrderHeaderRepository OrderHeader { get; private set; }
		public IOrderDetailRepository OrderDetail { get; private set; }
		public IProductImageRepository ProductImage { get; private set; }
        public IPackagingRepository Packaging { get; private set; }
		public IPackagingByCategoryRepository PackagingByCategory { get; private set; }
		public IUnitPackagingByCategoryRepository UnitPackagingByCategory { get; private set; }
		public IFabricRepository Fabric { get; private set; }
		public IFabricByProductRepository FabricByProduct { get; private set; }
		public IUnitFabricByProductRepository UnitFabricByProduct{ get; private set; }
        public IGarmentHardwareRepository GarmentHardware { get; private set; }
		public IGarmentHardwareByProductRepository GarmentHardwareByProduct { get; private set; }
		public IUnitGarmentHardwareByProductRepository UnitGarmentHardwareByProduct { get; private set; }
		public IFixedCostRepository FixedCost { get; private set; }
		public IPercentageCostRepository PercentageCost { get; private set; }
		public IPercentageCostWholesaleRepository PercentageCostWholesale { get; private set; }
        public IPercentageProfitRepository PercentageProfit { get; }
		public IFavoriteProductRepository FavoriteProduct { get; private set; }

		public UnitOfWork(ApplicationDbContext db) 
        {
            _db = db;
            Category = new CategoryRepository(_db);
            Product = new ProductRepository(_db);
            Company = new CompanyRepository(_db);
            ShoppingCart = new ShoppingCartRepository(_db);
            ApplicationUser = new ApplicationUserRepository(_db);
            OrderHeader = new OrderHeaderRepository(_db);
            OrderDetail = new OrderDetailRepository(_db);
            ProductImage = new ProductImageRepository(_db);
            Packaging= new PackagingRepository(_db);
			PackagingByCategory = new PackagingByCategoryRepository(_db);
			UnitPackagingByCategory = new UnitPackagingByCategoryRepository(_db);
            Fabric = new FabricRepository(_db);
            FabricByProduct= new FabricByProductRepository(_db);
            UnitFabricByProduct= new UnitFabricByProductRepository(_db);
			GarmentHardware = new GarmentHardwareRepository(_db);
			GarmentHardwareByProduct = new GarmentHardwareByProductRepository(_db);
            UnitGarmentHardwareByProduct = new UnitGarmentHardwareByProductRepository(_db);
			FixedCost = new FixedCostRepository(_db);
			PercentageCost = new PercentageCostRepository(_db);
			PercentageCostWholesale = new PercentageCostWholesaleRepository(_db);
            PercentageProfit= new PercentageProfitRepository(_db);
			FavoriteProduct = new FavoriteProductRepository(_db);

		}

        public void Save()
        {
            _db.SaveChanges();
        }

		public void ExecuteInTransaction(Action operation)
		{
			using var transaction = _db.Database.BeginTransaction();
			try
			{
				operation();
				transaction.Commit();
			}
			catch
			{
				transaction.Rollback();
				throw;
			}
		}

		public void UpdateEntityValues<TEntity>(TEntity existingEntity, TEntity newValues) where TEntity : class
		{

			_db.Entry(existingEntity).CurrentValues.SetValues(newValues);
		}
	}
}
