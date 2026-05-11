using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UkiyoDesigns.Models.CalculatorModels;

namespace UkiyoDesigns.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Product Name is required")]
        public string Name { get; set; }

        [Required]
        public string Description { get; set; }


        [Display(Name = "Calculate List Price")]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal ListPrice { get; set; }


        [Required]
        [Display(Name = "Minor Final Price")]
        [Range(1000, 1000000)]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal FinalRetailPrice { get; set; }

        
        [Display(Name = "Major Final Price")]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal FinalWholesalePrice { get; set; }

        public bool IsAvailableInStore { get; set; } = false;
        public bool IsDeleted { get; set; } = false;

		public int CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        [ValidateNever]
        public Category Category { get; set; }
		[ValidateNever]
		public List<ProductImage> ProductImages { get; set; }
		[ValidateNever]
		public FabricByProduct FabricByProduct { get; set; }
		[ValidateNever]
		public GarmentHardwareByProduct GarmentHardwareByProduct { get; set; }
		
	}
}
