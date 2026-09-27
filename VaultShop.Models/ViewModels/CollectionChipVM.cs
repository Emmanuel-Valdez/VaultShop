namespace VaultShop.Models.ViewModels
{
	public class CollectionChipVM
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public string Slug { get; set; } = string.Empty;
		public string? ChipImageUrl { get; set; }
		public string? CoverImageUrl { get; set; }
		public string? MediumCoverImageUrl { get; set; }
		public string? SmallCoverImageUrl { get; set; }
		public int Count { get; set; }
	}
}