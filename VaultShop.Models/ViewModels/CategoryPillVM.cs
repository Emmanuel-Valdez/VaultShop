namespace VaultShop.Models.ViewModels
{
    public class CategoryPillVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        // product-slugs 2.1: Slug is the category's own slug (cslug param); CollectionSlug rides along
        // so additive filtering (category chip while a collection is active) keeps both canonical.
        public string? Slug { get; set; }
        public string? CollectionSlug { get; set; }
        public bool IsActive { get; set; }
    }
}
