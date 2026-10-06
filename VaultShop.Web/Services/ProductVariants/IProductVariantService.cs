using VaultShop.Models;

namespace VaultShop.Web.Services.ProductVariants
{
	public interface IProductVariantService
	{
		VariantAdminData? GetAdminData(int productId);
		// ponytail: lean storefront read — types + combinations only, no admin extras (AllTypeNames/Product).
		// Null when the product defines no values (variant-less → no selectors).
		VariantSelectionData? GetSelectionData(int productId);
		VariantResult AddValue(int productId, string typeName, string value, int sortOrder);
		// hardening 2.1/2.2: productId scopes the mutation — cross-product ids are rejected,
		// and DeleteVariant refuses while cart lines reference the variant.
		VariantResult DeleteValue(int productId, int valueId);
		VariantResult GenerateCombinations(int productId);
		VariantResult SetAvailability(int productId, int variantId, bool isAvailable);
		VariantResult DeleteVariant(int productId, int variantId);
		VariantValidationResult ValidateVariantForProduct(int productId, int? variantId);
		string BuildVariantLabel(int variantId);

		public sealed class VariantAdminData
		{
			public Product Product { get; init; } = null!;
			public List<string> AllTypeNames { get; init; } = new();
			public List<VariantTypeGroup> Types { get; init; } = new();
			public List<VariantRow> Variants { get; init; } = new();
		}

		public sealed class VariantTypeGroup
		{
			public int TypeId { get; init; }
			public string TypeName { get; init; } = string.Empty;
			public List<VariantOptionValue> Values { get; init; } = new();
		}

		public sealed class VariantRow
		{
			public ProductVariant Variant { get; init; } = null!;
			public string Label { get; init; } = string.Empty;
		}

		public sealed class VariantSelectionData
		{
			public List<VariantTypeGroup> Types { get; init; } = new();
			public List<VariantRow> Variants { get; init; } = new();
		}

		public sealed class VariantResult
		{
			public bool Success { get; init; }
			public string? ErrorKey { get; init; }
			public int CreatedCount { get; init; }
			public static VariantResult Ok(int createdCount = 0) => new() { Success = true, CreatedCount = createdCount };
			public static VariantResult Fail(string errorKey) => new() { Success = false, ErrorKey = errorKey };
		}

		public sealed class VariantValidationResult
		{
			public bool IsValid { get; init; }
			public string? ErrorKey { get; init; }
			public static VariantValidationResult Valid() => new() { IsValid = true };
			public static VariantValidationResult Invalid(string errorKey) => new() { IsValid = false, ErrorKey = errorKey };
		}
	}
}
