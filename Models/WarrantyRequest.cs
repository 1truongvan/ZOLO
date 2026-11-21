namespace WebApplication1.Models;

public class WarrantyRequest
{
    public int Id { get; set; }
    public string RequestCode { get; set; } = string.Empty;
    public int CustomerUserId { get; set; }
    public int? ProductId { get; set; }
    public int? AssignedTechnicianId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Submitted";
    public string? InspectionResult { get; set; }
    public string? FaultCause { get; set; }
    public string? RepairNotes { get; set; }
    public string? Resolution { get; set; }
    public string? ManagerNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public User Customer { get; set; } = null!;
    public User? AssignedTechnician { get; set; }
    public Product? Product { get; set; }
}
