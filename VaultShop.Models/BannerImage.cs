using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VaultShop.Models
{
	public enum BannerImageKind
	{
		Master = 0,
		Medium = 1,
		Small = 2,
		Mobile = 3
	}

	public class BannerImage
	{
		public int Id { get; set; }

		public BannerImageKind Kind { get; set; }

		[Required]
		public string ImageUrl { get; set; } = string.Empty;

		public string ObjectKey { get; set; } = string.Empty;

		public string FileName { get; set; } = string.Empty;

		public string ContentType { get; set; } = string.Empty;

		public long SizeBytes { get; set; }

		public string StorageProvider { get; set; } = string.Empty;

		public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

		public int BannerId { get; set; }

		[ForeignKey("BannerId")]
		public Banner Banner { get; set; } = null!;
	}
}
