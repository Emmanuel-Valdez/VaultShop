using System.ComponentModel.DataAnnotations;

namespace VaultShop.Models
{
    public class PostalAgency
    {
        [Key]
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public int? Number { get; set; }
        public string Locality { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
        public string ProvinceCode { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime? LastVerifiedUtc { get; set; }
        public string? Source { get; set; }
    }
}
