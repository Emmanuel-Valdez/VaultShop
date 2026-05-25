using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UkiyoDesigns.Models.CalculatorModels;
using UkiyoDesigns.Models.Validation;

namespace UkiyoDesigns.Models
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
        public bool IsDeleted { get; set; } = false;

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
