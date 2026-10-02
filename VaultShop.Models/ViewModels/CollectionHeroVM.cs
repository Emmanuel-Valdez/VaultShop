namespace VaultShop.Models.ViewModels
{
	public class CollectionHeroVM
	{
		public string ImageUrl { get; set; } = string.Empty;
		public string? SmallImageUrl { get; set; }
		public string? MediumImageUrl { get; set; }
		public string Title { get; set; } = string.Empty;
		public int Count { get; set; }
	}
}
