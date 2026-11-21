using System.ComponentModel.DataAnnotations;

namespace WebApplication1.DTOs;

public class AdminProductRequest
{
    [Required, StringLength(60)] public string ProductCode { get; set; } = string.Empty;
    [Required, StringLength(150)] public string ProductName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Category { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Brand { get; set; } = string.Empty;
    [StringLength(100)] public string Model { get; set; } = string.Empty;
    [StringLength(100)] public string SerialNumber { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    [Range(0, 1200)] public int WarrantyPeriod { get; set; }
    [Range(1, int.MaxValue)] public int? OwnerUserId { get; set; }
}
