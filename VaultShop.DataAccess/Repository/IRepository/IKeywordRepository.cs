using VaultShop.Models;

namespace VaultShop.DataAccess.Repository.IRepository
{
	public interface IKeywordRepository : IRepository<Keyword>
	{
		void Update(Keyword obj);
	}
}