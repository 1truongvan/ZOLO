namespace WebApplication1.Models;

public class StockDocument
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public int? StationId { get; set; }
    public int? DealerId { get; set; }
    public string? Counterparty { get; set; }
    public string Status { get; set; } = "Draft";
    public string? Notes { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; set; }
    public AdminCatalogEntry Warehouse { get; set; } = null!;
    public AdminCatalogEntry? Station { get; set; }
    public AdminCatalogEntry? Dealer { get; set; }
    public User CreatedBy { get; set; } = null!;
    public ICollection<StockDocumentLine> Lines { get; set; } = new List<StockDocumentLine>();
}
