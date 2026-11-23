using System.ComponentModel.DataAnnotations;
using WebApplication1.Models;

namespace YourProject.Models
{
    public class RepairJobUpdate
    {
        public int Id { get; set; }
        public int RepairJobId { get; set; }
        public int UpdatedByUserId { get; set; }

        [MaxLength(30)]
        public string? OldStatus { get; set; }

        [Required]
        [MaxLength(30)]
        public string NewStatus { get; set; } = string.Empty;

        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public RepairJob RepairJob { get; set; } = null!;
        public User UpdatedByUser { get; set; } = null!;
    }
}
