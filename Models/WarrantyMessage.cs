using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class WarrantyMessage
    {
        public int Id { get; set; }

        public int WarrantyRequestId { get; set; }
        public int SenderUserId { get; set; }

        [Required]
        [StringLength(2000, MinimumLength = 1)]
        public string Content { get; set; } = string.Empty;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public WarrantyRequest WarrantyRequest { get; set; } = null!;
        public User Sender { get; set; } = null!;
    }
}
