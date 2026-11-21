using System.ComponentModel.DataAnnotations;
namespace WebApplication1.DTOs;
public class StockDocumentRequest
{
    [Required] public string Type { get; set; } = string.Empty;
    [Range(1,int.MaxValue)] public int WarehouseId { get; set; }
    public int? StationId { get; set; }
    public int? DealerId { get; set; }
    [StringLength(180)] public string? Counterparty { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    [MinLength(1)] public List<StockDocumentLineRequest> Lines { get; set; } = [];
}
public class StockDocumentLineRequest
{
    public int? ProductId { get; set; }
    public int? ComponentId { get; set; }
    [Range(1,1000000)] public int Quantity { get; set; }
    [Range(0,100000000000)] public decimal UnitPrice { get; set; }
}
