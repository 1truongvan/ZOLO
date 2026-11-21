using System.ComponentModel.DataAnnotations;

namespace WebApplication1.DTOs;

public class AdminCatalogRequest
{
    [Required, StringLength(60, MinimumLength = 1)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(150, MinimumLength = 1)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    [StringLength(500)] public string? LogoUrl { get; set; }
    [StringLength(250)] public string? Address { get; set; }
    [StringLength(30)] public string? ContactPhone { get; set; }
    [StringLength(30)] public string? PhoneNumber { get; set; }
    [Range(1, int.MaxValue)] public int? ResponsibleUserId { get; set; }
    public int? ManagerUserId { get; set; }
    [StringLength(100)] public string? ProductGroup { get; set; }
    [StringLength(100)] public string? FaultGroup { get; set; }
    [StringLength(100)] public string? Region { get; set; }
    [StringLength(40)] public string? Unit { get; set; }
    [Range(0, 100000000000)] public decimal? Amount { get; set; }
    [Range(0, 1000000)] public decimal? RangeFrom { get; set; }
    [Range(0, 1000000)] public decimal? RangeTo { get; set; }
    [Range(0, 1200)] public int? DurationMonths { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public bool IsActive { get; set; } = true;
}
