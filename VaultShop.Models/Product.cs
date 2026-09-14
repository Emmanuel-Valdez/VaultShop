using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VaultShop.Models.CalculatorModels;
using VaultShop.Models.Validation;

namespace VaultShop.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

		[LocalizedRequired("Product name is required.", "El nombre del producto es obligatorio.")]
		public string Name { get; set; } = string.Empty;

		[LocalizedRequired("Product description is required.", "La descripción del producto es obligatoria.")]
		public string Description { get; set; } = string.Empty;


        [Display(Name = "Calculate List Price")]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal ListPrice { get; set; }


        [LocalizedRequired("Retail price is required.", "El precio minorista es obligatorio.")]
        [Display(Name = "Minor Final Price")]
		[LocalizedRange(1000, 1000000, "Please enter a retail price between 1000 and 1000000.", "Ingresá un precio minorista entre 1000 y 1000000.")]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal FinalRetailPrice { get; set; }

        
        [Display(Name = "Major Final Price")]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal FinalWholesalePrice { get; set; }

        public bool IsAvailableInStore { get; set; } = false;
        public bool IsFeatured { get; set; } = false;
        public int FeaturedSortOrder { get; set; } = 0;
        public bool IsDeleted { get; set; } = false;

        [LocalizedRange(0, int.MaxValue, "Stock quantity cannot be negative.", "La cantidad en stock no puede ser negativa.")]
        public int StockQuantity { get; set; } = 0;

        [LocalizedRequired("Monthly expectation is required.", "La expectativa mensual es obligatoria.")]
        [DisplayName("Max Expectation Monthly")]
        [LocalizedRange(1, 10000, "Please enter a monthly expectation between 1 and 10000.", "Ingresá una expectativa mensual entre 1 y 10000.")]
        public int MaxExpectation { get; set; }

		[LocalizedRequired("Category is required.", "La categoría es obligatoria.")]
		[LocalizedRange(1, int.MaxValue, "Category is required.", "La categoría es obligatoria.")]
		public int CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        [ValidateNever]
		public Category Category { get; set; } = null!;
		[ValidateNever]
		public List<ProductImage> ProductImages { get; set; } = new();
		[ValidateNever]
		public FabricByProduct FabricByProduct { get; set; } = null!;
		[ValidateNever]
		public GarmentHardwareByProduct GarmentHardwareByProduct { get; set; } = null!;
		
	}
}
