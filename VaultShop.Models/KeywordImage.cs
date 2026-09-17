using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VaultShop.Models
{
	public enum KeywordImageKind
	{
		Chip = 0,
		Cover = 1
	}

	public class KeywordImage
	{
		public int Id { get; set; }

		public KeywordImageKind Kind { get; set; }

		[Required]
		public string ImageUrl { get; set; } = string.Empty;

		public string ObjectKey { get; set; } = string.Empty;

		public string FileName { get; set; } = string.Empty;

		public string ContentType { get; set; } = string.Empty;

		public long SizeBytes { get; set; }

		public string StorageProvider { get; set; } = string.Empty;

		public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

		public int KeywordId { get; set; }

		[ForeignKey("KeywordId")]
		public Keyword Keyword { get; set; } = null!;
	}
}