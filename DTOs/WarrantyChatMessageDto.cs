namespace WebApplication1.DTOs
{
    public class WarrantyChatMessageDto
    {
        public int Id { get; set; }
        public int WarrantyRequestId { get; set; }
        public int SenderUserId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
    }
}
