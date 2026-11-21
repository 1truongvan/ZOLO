using System.ComponentModel.DataAnnotations;
namespace WebApplication1.DTOs;
public class WarrantyRequestCreateDto { [Range(1,int.MaxValue)] public int ProductId { get; set; } [Required,StringLength(2000,MinimumLength=5)] public string Description { get; set; }=string.Empty; }
public class WarrantyReviewDto { public bool Approved { get; set; } [StringLength(1000)] public string? Note { get; set; } }
public class WarrantyAssignmentDto { [Range(1,int.MaxValue)] public int TechnicianId { get; set; } }
public class WarrantyRepairUpdateDto { [Required,StringLength(2000)] public string InspectionResult { get; set; }=string.Empty; [StringLength(1000)] public string? FaultCause { get; set; } [StringLength(2000)] public string? RepairNotes { get; set; } [StringLength(2000)] public string? Resolution { get; set; } }
public class WarrantyCompletionDto { public bool Completed { get; set; } [StringLength(1000)] public string? Note { get; set; } }
