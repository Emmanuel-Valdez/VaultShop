namespace UkiyoDesigns.Models.ViewModels
{
	public class HomeIndexVM
	{
		public IEnumerable<Product> Products { get; set; } = [];
		public IEnumerable<Product> FeaturedProducts { get; set; } = [];
	}
}
