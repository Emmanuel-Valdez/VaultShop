using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VaultShop.Models.Validation;

namespace VaultShop.Models
{
	public enum CouponDiscountType
	{
		Percent = 0,
		FixedAmount = 1
	}

	public class Coupon
	{
		[Key]
		public int Id { get; set; }

		[LocalizedRequired("Coupon code is required.", "El código del cupón es obligatorio.")]
		[MaxLength(50)]
		public string Code { get; set; } = string.Empty;

		public CouponDiscountType DiscountType { get; set; } = CouponDiscountType.Percent;

		[Column(TypeName = "decimal(18, 2)")]
		public decimal Value { get; set; }

		// Optional minimum order subtotal for the coupon to apply. Null = no minimum.
		[Column(TypeName = "decimal(18, 2)")]
		public decimal? MinSubtotal { get; set; }

		public DateTime? ValidFromUtc { get; set; }

		public DateTime? ValidToUtc { get; set; }

		// Null = unlimited uses.
		public int? MaxUses { get; set; }

		public int UsesCount { get; set; } = 0;

		public bool IsActive { get; set; } = true;
	}
}
