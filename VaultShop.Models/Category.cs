using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VaultShop.Models.CalculatorModels;
using VaultShop.Models.Validation;

namespace VaultShop.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }
        [LocalizedRequired("Category name is required.", "El nombre de la categoría es obligatorio.")]
        [DisplayName("Category Name")]
		public string Name { get; set; } = string.Empty;

		public bool IsDeleted { get; set; } = false;
		[LocalizedRequired("Average shipping cost is required.", "El costo promedio de envío es obligatorio.")]
        [LocalizedRange(1, 100000, "Please enter an average shipping cost between 1 and 100000.", "Ingresá un costo promedio de envío entre 1 y 100000.")]
        [DisplayName("Avg Shipping Cost")]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal AvgShippingCost { get; set; }

		public string? ImageUrl { get; set; }
		public string? ObjectKey { get; set; }
		public string? FileName { get; set; }
		public string? ContentType { get; set; }
		public long? SizeBytes { get; set; }
		public string? StorageProvider { get; set; }


        [ValidateNever]
		public PackagingByCategory PackagingByCategory { get; set; } = null!;

	}
}
