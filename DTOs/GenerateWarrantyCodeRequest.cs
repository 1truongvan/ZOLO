using System.ComponentModel.DataAnnotations;

namespace WebApplication1.DTOs
{
    public class GenerateWarrantyCodeRequest
    {
        [Required]
        [MaxLength(120)]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        public string Brand { get; set; } = string.Empty;

        [Required]
        public string Category { get; set; } = string.Empty;

        [Required]
        public string Model { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string SerialNumber { get; set; } = string.Empty;

        public DateTime PurchaseDate { get; set; }

        [Range(0, 120)]
        public int WarrantyPeriod { get; set; }
    }
}
