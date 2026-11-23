using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WebApplication1.Models;

namespace YourProject.Models
{
    public class RepairJobPart
    {
        public int Id { get; set; }
        public int RepairJobId { get; set; }
        public int ComponentId { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public RepairJob RepairJob { get; set; } = null!;
        public AdminCatalogEntry Component { get; set; } = null!;
    }
}
