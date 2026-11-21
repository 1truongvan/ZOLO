namespace WebApplication1.Models;

public class StockDocumentLine
{
    public int Id { get; set; }
    public int StockDocumentId { get; set; }
    public int? ProductId { get; set; }
    public int? ComponentId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public StockDocument Document { get; set; } = null!;
    public Product? Product { get; set; }
    public AdminCatalogEntry? Component { get; set; }
}
