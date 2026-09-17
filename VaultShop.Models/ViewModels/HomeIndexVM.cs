using VaultShop.Models.Pagination;

namespace VaultShop.Models.ViewModels
{
	public class HomeIndexVM
	{
		public PagedList<Product> Products { get; set; } = PagedList<Product>.Create([], 1, 12);
		public IEnumerable<Product> FeaturedProducts { get; set; } = [];
		public IEnumerable<Category> Categories { get; set; } = [];
		public List<CollectionChipVM> Collections { get; set; } = [];

		// Single in-memory pass over the already-loaded visible product list (design D7):
		// counter = distinct in-stock products per keyword; chip image from the keyword's Chip image.
		public static List<CollectionChipVM> ComputeCollections(IEnumerable<Product> products)
		{
			return products
				.SelectMany(p => p.Keywords.Select(pk => (Keyword: pk.Keyword, InStock: p.StockQuantity > 0)))
				.Where(x => x.Keyword != null && !x.Keyword.IsDeleted)
				.GroupBy(x => x.Keyword.Id)
				.Select(g => new CollectionChipVM
				{
					Id = g.Key,
					Name = g.First().Keyword.Name,
					Slug = g.First().Keyword.Slug,
					ChipImageUrl = g.First().Keyword.Images.FirstOrDefault(i => i.Kind == KeywordImageKind.Chip)?.ImageUrl,
					Count = g.Count(x => x.InStock)
				})
				.OrderBy(c => c.Name)
				.ToList();
		}
	}
}
