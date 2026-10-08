using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VaultShop.Models.Validation;

namespace VaultShop.Models
{
	public enum PromotionKind
	{
		// Buy-X-get-Y (2x1, 3x2, second unit 50% off).
		BxGy = 0,
		// Percentage off scoped to product/category/keyword/store.
		PercentOff = 1,
		// Percentage off for a payment method (e.g. bank transfer -10%).
		PaymentMethodDiscount = 2
	}

	public enum PromotionScope
	{
		Store = 0,
		Product = 1,
		Category = 2,
		Keyword = 3
	}

	public class Promotion
	{
		[Key]
		public int Id { get; set; }

		[LocalizedRequired("Promotion name is required.", "El nombre de la promoción es obligatorio.")]
		[MaxLength(120)]
		public string Name { get; set; } = string.Empty;

		public PromotionKind Kind { get; set; } = PromotionKind.BxGy;

		public PromotionScope Scope { get; set; } = PromotionScope.Store;

		// Scope targets; only the one matching Scope is set.
		public int? ProductId { get; set; }
		[ForeignKey("ProductId")]
		[ValidateNever]
		public Product? Product { get; set; }

		public int? CategoryId { get; set; }
		[ForeignKey("CategoryId")]
		[ValidateNever]
		public Category? Category { get; set; }

		public int? KeywordId { get; set; }
		[ForeignKey("KeywordId")]
		[ValidateNever]
		public Keyword? Keyword { get; set; }

		// PaymentMethodDiscount only: e.g. "BankTransfer". Null otherwise.
		[MaxLength(50)]
		public string? PaymentMethod { get; set; }

		// BxGy: BuyQty + GetQty form the group size; GetQty units get
		// GetDiscountPercent off (100 = free). 2x1 = Buy 1 Get 1 free.
		public int BuyQty { get; set; } = 0;

		public int GetQty { get; set; } = 0;

		[Column(TypeName = "decimal(18, 2)")]
		public decimal GetDiscountPercent { get; set; } = 100;

		// PercentOff + PaymentMethodDiscount value.
		[Column(TypeName = "decimal(18, 2)")]
		public decimal DiscountPercent { get; set; } = 0;

		// BxGy only: group must come from the same SKU when true,
		// otherwise mixed products within scope combine.
		public bool SameSkuOnly { get; set; } = false;

		// Wholesale (Company role / wholesale preview) lines are excluded
		// from specific discounts unless the promotion opts in.
		public bool IncludeWholesale { get; set; } = false;

		public DateTime? ValidFromUtc { get; set; }

		public DateTime? ValidToUtc { get; set; }

		public bool IsActive { get; set; } = true;
	}
}
