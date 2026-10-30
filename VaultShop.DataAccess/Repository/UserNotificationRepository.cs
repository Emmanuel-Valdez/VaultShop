using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;

namespace VaultShop.DataAccess.Repository
{
	public class UserNotificationRepository : Repository<UserNotification>, IUserNotificationRepository
	{
		public UserNotificationRepository(ApplicationDbContext db) : base(db)
		{
		}
	}
}
