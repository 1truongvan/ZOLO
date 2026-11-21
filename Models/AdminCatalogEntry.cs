using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models;

public class AdminCatalogEntry
{
    public int Id { get; set; }
    public string ResourceKey { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? Address { get; set; }
    public string? ContactPhone { get; set; }
    public int? ResponsibleUserId { get; set; }
    [NotMapped] public int? ManagerUserId { get => ResponsibleUserId; set => ResponsibleUserId = value; }
    [NotMapped] public string? PhoneNumber { get => ContactPhone; set => ContactPhone = value; }
    public string? ProductGroup { get; set; }
    public string? FaultGroup { get; set; }
    public string? Region { get; set; }
    public string? Unit { get; set; }
    public decimal? Amount { get; set; }
    public decimal? RangeFrom { get; set; }
    public decimal? RangeTo { get; set; }
    public int? DurationMonths { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public User? ResponsibleUser { get; set; }
}
