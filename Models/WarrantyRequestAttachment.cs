namespace WebApplication1.Models;

public class WarrantyRequestAttachment
{
    public int Id { get; set; }
    public int WarrantyRequestId { get; set; }
    public int StoredFileId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public WarrantyRequest WarrantyRequest { get; set; } = null!;
    public StoredFile StoredFile { get; set; } = null!;
}
