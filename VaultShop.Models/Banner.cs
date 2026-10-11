using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace VaultShop.Models
{
	public enum BannerLayout
	{
		Crossed = 0,
		ImageLed = 1
	}

	public enum BannerTextColor
	{
		BrandLight = 0,
		BrandDark = 1,
		White = 2,
		Black = 3
	}

	public enum BannerTargetKind
	{
		Store = 0,
		Category = 1,
		Collection = 2,
		Product = 3
	}

	public class Banner
	{
		[Key]
		public int Id { get; set; }

		public BannerLayout Layout { get; set; } = BannerLayout.Crossed;

		public BannerTextColor TextColor { get; set; } = BannerTextColor.White;

		[MaxLength(120)]
		public string? Title { get; set; }

		[MaxLength(200)]
		public string? Hook { get; set; }

		[MaxLength(500)]
		public string? Body { get; set; }

		[MaxLength(60)]
		public string? CallToAction { get; set; }

		[MaxLength(200)]
		public string? AltText { get; set; }

		public BannerTargetKind TargetKind { get; set; } = BannerTargetKind.Store;

		public int? TargetId { get; set; }

		public int SortOrder { get; set; } = 0;

		public bool IsActive { get; set; } = true;

		[ValidateNever]
		public List<BannerImage> Images { get; set; } = new();
	}
}
