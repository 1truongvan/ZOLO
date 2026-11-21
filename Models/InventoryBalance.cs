namespace WebApplication1.Models;

public class InventoryBalance
{
    public int Id { get; set; }
    public int WarehouseId { get; set; }
    public int? ProductId { get; set; }
    public int? ComponentId { get; set; }
    public int Quantity { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public AdminCatalogEntry Warehouse { get; set; } = null!;
    public Product? Product { get; set; }
    public AdminCatalogEntry? Component { get; set; }
}
