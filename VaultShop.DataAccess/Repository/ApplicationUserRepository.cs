using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;

namespace VaultShop.DataAccess.Repository
{
	public class ApplicationUserRepository : Repository<ApplicationUser>, IApplicationUserRepository
	{
		private ApplicationDbContext _db;
	
		public ApplicationUserRepository(ApplicationDbContext db) : base(db)
		{
			_db = db;
		}
		public void Update(ApplicationUser applicationUser)
		{
			_db.ApplicationUsers.Update(applicationUser);
		}


	}
}
