namespace VaultShop.Models.ViewModels
{
	public sealed record BranchOption(string Code, string Name, string Address, string Locality, string Province, string Hours);

	public class BranchCascadePickerVM
	{
		public string FieldName { get; set; } = "OrderHeader.PickupAgencyCode";
		public string IdPrefix { get; set; } = "branchPicker";
		public string SubmitButtonId { get; set; } = "placeOrderBtn";
		// Checkout collapses the branch picker into the domicile province → locality selects;
		// admin correction keeps the picker self-contained.
		public bool ShowProvinceLocalitySelectors { get; set; } = true;
		public string? ProvinceSelectId { get; set; }
		public string? LocalitySelectId { get; set; }
		public string ProvincesUrl { get; set; } = string.Empty;
		public string LocalitiesUrl { get; set; } = string.Empty;
		public string BranchesUrl { get; set; } = string.Empty;
		public IReadOnlyList<(string Code, string Name)> Provinces { get; set; } = [];
		public string SelectedProvinceCode { get; set; } = string.Empty;
		public IReadOnlyList<string> Localities { get; set; } = [];
		public string SelectedLocality { get; set; } = string.Empty;
		public IReadOnlyList<BranchOption> Branches { get; set; } = [];
		public string SelectedBranchCode { get; set; } = string.Empty;
		// ponytail: checkout requires a branch; admin correction is optional and must not block sibling actions.
		// IsRequired drives the radios' HTML required; RequiresBranch drives the submit gate (checkout toggles it per modality).
		public bool IsRequired { get; set; } = true;
		public bool RequiresBranch { get; set; } = true;
	}
}
