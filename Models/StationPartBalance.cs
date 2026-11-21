namespace WebApplication1.Models;

public class StationPartBalance
{
    public int Id { get; set; }
    public int StationId { get; set; }
    public int ComponentId { get; set; }
    public int AllocatedQuantity { get; set; }
    public int UsedQuantity { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public AdminCatalogEntry Station { get; set; } = null!;
    public AdminCatalogEntry Component { get; set; } = null!;
}
