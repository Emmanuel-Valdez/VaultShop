namespace VaultShop.Models.ViewModels
{
	// ponytail: flattened link (Models cannot reference the Web-side BannerTargetLink record).
	public class BannerSlideVM
	{
		public Banner Banner { get; set; } = new();

		public string? LinkAction { get; set; }

		public string? LinkController { get; set; }

		public string? LinkArea { get; set; }

		public Dictionary<string, string> LinkRouteValues { get; set; } = new();

		public bool HasLink => LinkAction != null;
	}
}
