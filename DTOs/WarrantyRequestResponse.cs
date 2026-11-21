namespace WebApplication1.DTOs;

public sealed class WarrantyRequestResponse
{
    public int Id { get; set; }
    public string RequestCode { get; set; } = string.Empty;
    public int CustomerUserId { get; set; }
    public WarrantyRequestPersonResponse? Customer { get; set; }
    public int? ProductId { get; set; }
    public WarrantyRequestProductResponse? Product { get; set; }
    public int? AssignedTechnicianId { get; set; }
    public WarrantyRequestPersonResponse? AssignedTechnician { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? InspectionResult { get; set; }
    public string? FaultCause { get; set; }
    public string? RepairNotes { get; set; }
    public string? Resolution { get; set; }
    public string? ManagerNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class WarrantyRequestPersonResponse
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Email { get; set; }
}

public sealed class WarrantyRequestProductResponse
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
}
