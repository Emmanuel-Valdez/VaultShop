using VaultShop.Models;

namespace VaultShop.DataAccess.Repository.IRepository
{
	public interface IKeywordImageRepository : IRepository<KeywordImage>
	{
		void Update(KeywordImage obj);
	}
}