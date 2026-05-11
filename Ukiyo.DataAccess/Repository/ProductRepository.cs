using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models;

namespace UkiyoDesigns.DataAccess.Repository
{
    public class ProductRepository : Repository<Product>, IProductRepository
    {
        private ApplicationDbContext _db;

        public ProductRepository(ApplicationDbContext db): base(db) 
        {
            _db = db;
        }

        public void Update(Product obj)
        {

            var objFromDB = _db.Products.FirstOrDefault(u => u.Id == obj.Id);
            if (objFromDB !=null)
            {
				objFromDB.Name = obj.Name;
				objFromDB.Description = obj.Description;
				objFromDB.ListPrice = obj.ListPrice;
                objFromDB.FinalWholesalePrice = obj.FinalWholesalePrice;
				objFromDB.FinalRetailPrice = obj.FinalRetailPrice;
				objFromDB.CategoryId = obj.CategoryId;
                objFromDB.ProductImages = obj.ProductImages;
                objFromDB.IsDeleted = obj.IsDeleted;
                objFromDB.IsAvailableInStore= obj.IsAvailableInStore;
                //if(obj.ImageUrl!= null)
                //{
                //    objFromDB.ImageUrl = obj.ImageUrl;
                //}
			}
        }
        public void UpdateRange(IEnumerable<Product> obj)
        {
            _db.Products.UpdateRange(obj);
        }
    }
}
