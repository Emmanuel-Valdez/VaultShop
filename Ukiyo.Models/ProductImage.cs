using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UkiyoDesigns.Models
{
	public class ProductImage
	{
		public int Id { get; set; }

		[Required]
		public string ImageUrl { get; set; } = string.Empty;

		public string ObjectKey { get; set; } = string.Empty;

		public string FileName { get; set; } = string.Empty;

		public string ContentType { get; set; } = string.Empty;

		public long SizeBytes { get; set; }

		public string StorageProvider { get; set; } = string.Empty;

		public int SortOrder { get; set; }

		public bool IsPrimary { get; set; }

		public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

		public int ProductId { get; set; }

		[ForeignKey("ProductId")]
		public Product Product { get; set; } = null!;
	}
}
