using System.ComponentModel.DataAnnotations;
using WebApplication1.Models;

namespace YourProject.Models
{
    public class RepairJob
    {
        public int Id { get; set; }

        public int WarrantyRequestId { get; set; }
        public int? TechnicianUserId { get; set; }

        [Required]
        [MaxLength(50)]
        public string JobNumber { get; set; } = string.Empty;

        [MaxLength(30)]
        public string Status { get; set; } = "Received";

        public string? Diagnosis { get; set; }
        public string? RepairNote { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        public WarrantyRequest WarrantyRequest { get; set; } = null!;
        public User? Technician { get; set; }
    }
}