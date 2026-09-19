namespace VaultShop.Models.ViewModels
{
    public class CategoryPillVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string? Slug { get; set; }
        public bool IsActive { get; set; }
    }
}
