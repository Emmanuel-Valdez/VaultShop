using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UkiyoDesigns.Models.CalculatorModels;

namespace UkiyoDesigns.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [DisplayName("Category Name")]
		public string Name { get; set; } = string.Empty;

        [Required]
        [DisplayName("Max Expectation Monthly")]
        [Range(1, 1000)]
        public int MaxExpectation { get; set; }
		public bool IsDeleted { get; set; } = false;
		[Required]
        [Range(1, 100000)]
        [DisplayName("Avg Shipping Cost")]
		[Column(TypeName = "decimal(18, 2)")]
		public decimal AvgShippingCost { get; set; }


        [ValidateNever]
		public PackagingByCategory PackagingByCategory { get; set; } = null!;

	}
}
