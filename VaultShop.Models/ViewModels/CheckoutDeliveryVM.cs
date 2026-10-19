namespace VaultShop.Models.ViewModels
{
	// envio-gratis-o-cargo §4: checkout domicile + delivery modality.
	public sealed record DeliveryModalityOption(string Code, string Label, bool RequiresBranch, decimal Charge);

	public class DomicilePickerVM
	{
		public IReadOnlyList<(string Code, string Name)> Provinces { get; set; } = [];
		public string SelectedProvinceCode { get; set; } = string.Empty;
		public string SelectedProvinceName { get; set; } = string.Empty;
		public IReadOnlyList<string> Localities { get; set; } = [];
		public string SelectedLocality { get; set; } = string.Empty;
		public IReadOnlyList<DeliveryModalityOption> Modalities { get; set; } = [];
		public string SelectedModality { get; set; } = string.Empty;
	}
}
