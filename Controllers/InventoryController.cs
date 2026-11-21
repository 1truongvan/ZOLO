using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.DTOs;
using WebApplication1.Models;
using WebApplication1.Middleware;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/admin/inventory")]
[Authorize]
public class InventoryController(AppDbContext db) : ControllerBase
{
    private static readonly HashSet<string> Types = new(StringComparer.OrdinalIgnoreCase) { "receipt", "shipment", "component-receipt", "component-proposal", "allocation" };

    [HttpGet("balances")]
    [ModulePermission("inventory-balances")]
    [Authorize(Roles="Admin,Manager")]
    public async Task<IActionResult> Balances([FromQuery] string? search,[FromQuery] int? warehouseId,[FromQuery] int page=1,[FromQuery] int pageSize=25,CancellationToken ct=default)
    {
        page=Math.Max(1,page);pageSize=Math.Clamp(pageSize,1,100);
        var query=db.InventoryBalances.AsNoTracking().Include(b=>b.Warehouse).Include(b=>b.Product).Include(b=>b.Component).AsQueryable();
        if(warehouseId.HasValue)query=query.Where(b=>b.WarehouseId==warehouseId);
        if(!string.IsNullOrWhiteSpace(search))query=query.Where(b=>b.Warehouse.Name.Contains(search)||(b.Product!=null&&(b.Product.ProductName.Contains(search)||b.Product.ProductCode.Contains(search)))||(b.Component!=null&&(b.Component.Name.Contains(search)||b.Component.Code.Contains(search))));
        var total=await query.CountAsync(ct);var items=await query.OrderBy(b=>b.Warehouse.Name).ThenBy(b=>b.Product!.ProductName).Skip((page-1)*pageSize).Take(pageSize).Select(b=>new{b.Id,b.WarehouseId,Warehouse=b.Warehouse.Name,b.ProductId,Product=b.Product==null?null:b.Product.ProductName,b.ComponentId,Component=b.Component==null?null:b.Component.Name,b.Quantity,b.UpdatedAt}).ToListAsync(ct);
        return Ok(new{items,total,page,pageSize,totalPages=(int)Math.Ceiling(total/(double)pageSize)});
    }

    [HttpGet("transactions")]
    [ModulePermission("inventory-balances", "view")]
    [Authorize(Roles="Admin,Manager")]
    public async Task<IActionResult> Transactions([FromQuery]int? warehouseId,[FromQuery]int? productId,[FromQuery]int? componentId,[FromQuery]string? search,[FromQuery]int page=1,[FromQuery]int pageSize=25,CancellationToken ct=default)
    {
        if(productId.HasValue&&componentId.HasValue)return BadRequest(new{message="Chỉ chọn sản phẩm hoặc linh kiện, không chọn đồng thời cả hai."});
        page=Math.Max(1,page);pageSize=Math.Clamp(pageSize,1,100);
        var query=db.StockDocumentLines.AsNoTracking().Where(line=>line.Document.ConfirmedAt!=null
            &&(line.Document.Status=="Confirmed"||line.Document.Status=="Allocated")
            &&(!warehouseId.HasValue||line.Document.WarehouseId==warehouseId)
            &&(!productId.HasValue||line.ProductId==productId)
            &&(!componentId.HasValue||line.ComponentId==componentId));
        if(!string.IsNullOrWhiteSpace(search))query=query.Where(line=>line.Document.Code.Contains(search)||line.Document.Counterparty!=null&&line.Document.Counterparty.Contains(search));
        var total=await query.CountAsync(ct);
        var items=await query.OrderByDescending(line=>line.Document.ConfirmedAt).ThenByDescending(line=>line.Id)
            .Skip((page-1)*pageSize).Take(pageSize)
            .Select(line=>new{line.Id,DocumentId=line.Document.Id,DocumentCode=line.Document.Code,Type=line.Document.Type,Status=line.Document.Status,
                WarehouseId=line.Document.WarehouseId,Warehouse=line.Document.Warehouse.Name,Station=line.Document.Station==null?null:line.Document.Station.Name,
                Counterparty=line.Document.Counterparty,ItemId=line.ProductId??line.ComponentId,
                Item=line.Product==null?line.Component!.Name:line.Product.ProductName,
                QuantityDelta=line.Document.Type=="receipt"||line.Document.Type=="component-receipt"?line.Quantity:-line.Quantity,
                line.UnitPrice,ConfirmedAt=line.Document.ConfirmedAt,CreatedBy=line.Document.CreatedBy.Username})
            .ToListAsync(ct);
        return Ok(new{items,total,page,pageSize,totalPages=(int)Math.Ceiling(total/(double)pageSize)});
    }

    [HttpGet("station-parts")]
    [ModulePermission("inventory-balances")]
    [Authorize(Roles="Admin,Manager,Technician")]
    public async Task<IActionResult> StationParts([FromQuery]int? stationId,CancellationToken ct)=>Ok(await db.StationPartBalances.AsNoTracking().Include(b=>b.Station).Include(b=>b.Component).Where(b=>!stationId.HasValue||b.StationId==stationId).OrderBy(b=>b.Station.Name).Select(b=>new{b.Id,b.StationId,Station=b.Station.Name,b.ComponentId,Component=b.Component.Name,b.AllocatedQuantity,b.UsedQuantity,Available=b.AllocatedQuantity-b.UsedQuantity,b.UpdatedAt}).ToListAsync(ct));

    [HttpGet("documents")]
    [ModulePermission("inventory-documents")]
    [Authorize(Roles="Admin,Manager")]
    public async Task<IActionResult> Documents([FromQuery]string? type,[FromQuery]string? status,[FromQuery]string? search,[FromQuery]DateTime? from,[FromQuery]DateTime? to,[FromQuery]int page=1,[FromQuery]int pageSize=25,CancellationToken ct=default)
    {
        page=Math.Max(1,page);pageSize=Math.Clamp(pageSize,1,100);var query=db.StockDocuments.AsNoTracking().Include(d=>d.Warehouse).Include(d=>d.Lines).AsQueryable();
        if(!string.IsNullOrWhiteSpace(type))query=query.Where(d=>d.Type==type);if(!string.IsNullOrWhiteSpace(status))query=query.Where(d=>d.Status==status);if(!string.IsNullOrWhiteSpace(search))query=query.Where(d=>d.Code.Contains(search)|| (d.Counterparty!=null&&d.Counterparty.Contains(search)));if(from.HasValue)query=query.Where(d=>d.CreatedAt>=from);if(to.HasValue)query=query.Where(d=>d.CreatedAt<to.Value.Date.AddDays(1));
        var total=await query.CountAsync(ct);var items=await query.OrderByDescending(d=>d.CreatedAt).Skip((page-1)*pageSize).Take(pageSize).Select(d=>new{d.Id,d.Code,d.Type,d.WarehouseId,Warehouse=d.Warehouse.Name,d.Counterparty,d.Status,d.Notes,d.CreatedAt,d.ConfirmedAt,LineCount=d.Lines.Count,Total=d.Lines.Sum(l=>l.Quantity*l.UnitPrice)}).ToListAsync(ct);return Ok(new{items,total,page,pageSize,totalPages=(int)Math.Ceiling(total/(double)pageSize)});
    }

    [HttpGet("documents/{id:int}")]
    [ModulePermission("inventory-documents")]
    [Authorize(Roles="Admin,Manager")]
    public async Task<IActionResult> Document(int id,CancellationToken ct){var doc=await db.StockDocuments.AsNoTracking().Include(d=>d.Warehouse).Include(d=>d.Station).Include(d=>d.Dealer).Include(d=>d.Lines).ThenInclude(l=>l.Product).Include(d=>d.Lines).ThenInclude(l=>l.Component).FirstOrDefaultAsync(d=>d.Id==id,ct);return doc is null?NotFound():Ok(doc);}

    [HttpPost("documents")]
    [ModulePermission("inventory-documents")]
    [Authorize(Roles="Admin,Manager,Technician")]
    public async Task<IActionResult> Create(StockDocumentRequest request,CancellationToken ct)
    {
        var type=request.Type.Trim().ToLowerInvariant();if(!Types.Contains(type))return BadRequest(new{message="Loại phiếu không hợp lệ."});
        if(!await db.AdminCatalogEntries.AnyAsync(e=>e.Id==request.WarehouseId&&e.ResourceKey=="warehouses"&&e.IsActive,ct))return BadRequest(new{message="Kho không tồn tại hoặc đã ngừng hoạt động."});
        if(type=="allocation"&&(!request.StationId.HasValue||!await db.AdminCatalogEntries.AnyAsync(e=>e.Id==request.StationId&&e.ResourceKey=="stations"&&e.IsActive,ct)))return BadRequest(new{message="Cần chọn trạm sửa chữa hợp lệ."});
        if(type=="shipment"&&!request.DealerId.HasValue&&!string.IsNullOrWhiteSpace(request.Counterparty))request.DealerId=await db.AdminCatalogEntries.Where(e=>e.ResourceKey=="dealers"&&e.IsActive&&e.Name==request.Counterparty.Trim()).Select(e=>(int?)e.Id).FirstOrDefaultAsync(ct);
        if(type=="shipment"&&(!request.DealerId.HasValue||!await db.AdminCatalogEntries.AnyAsync(e=>e.Id==request.DealerId&&e.ResourceKey=="dealers"&&e.IsActive,ct)))return BadRequest(new{message="Nhập tên đại lý đang hoạt động đúng như danh mục Đại lý."});
        var componentOnly=type is "component-receipt" or "component-proposal" or "allocation";
        if(request.Lines.Count==0||request.Lines.Any(line=>line.Quantity<=0||line.UnitPrice<0||(componentOnly?(line.ComponentId is null||line.ProductId is not null):(line.ProductId is null||line.ComponentId is not null))))return BadRequest(new{message="Mỗi dòng phải có đúng một sản phẩm/linh kiện và số lượng hợp lệ."});
        if(request.Lines.GroupBy(line=>(line.ProductId,line.ComponentId)).Any(group=>group.Count()>1))return BadRequest(new{message="Mỗi sản phẩm/linh kiện chỉ được xuất hiện một lần trong một phiếu."});
        foreach(var line in request.Lines){if(line.ProductId.HasValue&&!await db.Products.AnyAsync(p=>p.Id==line.ProductId,ct))return BadRequest(new{message=$"Không tìm thấy sản phẩm {line.ProductId}."});if(line.ComponentId.HasValue&&!await db.AdminCatalogEntries.AnyAsync(e=>e.Id==line.ComponentId&&e.ResourceKey=="components",ct))return BadRequest(new{message=$"Không tìm thấy linh kiện {line.ComponentId}."});}
        var userId=User.FindFirstValue(ClaimTypes.NameIdentifier);if(!int.TryParse(userId,out var createdBy)||!await db.Users.AnyAsync(u=>u.Id==createdBy,ct))return Unauthorized();
        var doc=new StockDocument{Code=$"{type.ToUpperInvariant()}-{Guid.NewGuid():N}"[..40],Type=type,WarehouseId=request.WarehouseId,StationId=request.StationId,DealerId=request.DealerId,Counterparty=request.Counterparty?.Trim(),Notes=request.Notes?.Trim(),CreatedByUserId=createdBy,Status=type=="component-proposal"?"PendingApproval":"Draft",Lines=request.Lines.Select(line=>new StockDocumentLine{ProductId=line.ProductId,ComponentId=line.ComponentId,Quantity=line.Quantity,UnitPrice=line.UnitPrice}).ToList()};
        db.StockDocuments.Add(doc);await db.SaveChangesAsync(ct);return CreatedAtAction(nameof(Document),new{id=doc.Id},new{id=doc.Id,doc.Code,doc.Status});
    }

    [HttpPut("documents/{id:int}")]
    [ModulePermission("inventory-documents")]
    [Authorize(Roles="Admin,Manager")]
    public async Task<IActionResult> UpdateDraft(int id,StockDocumentRequest request,CancellationToken ct)
    {
        var doc=await db.StockDocuments.Include(d=>d.Lines).FirstOrDefaultAsync(d=>d.Id==id,ct);
        if(doc is null)return NotFound();
        if(doc.Status!="Draft"||doc.Type!=request.Type)return Conflict(new{message="Chỉ sửa được phiếu nháp cùng loại."});
        if(!await db.AdminCatalogEntries.AnyAsync(e=>e.Id==request.WarehouseId&&e.ResourceKey=="warehouses"&&e.IsActive,ct))return BadRequest(new{message="Kho không tồn tại hoặc đã ngừng hoạt động."});
        if(doc.Type=="shipment"&&(!request.DealerId.HasValue||!await db.AdminCatalogEntries.AnyAsync(e=>e.Id==request.DealerId&&e.ResourceKey=="dealers"&&e.IsActive,ct)))return BadRequest(new{message="Cần chọn đại lý đang hoạt động."});
        if(doc.Type=="allocation"&&(!request.StationId.HasValue||!await db.AdminCatalogEntries.AnyAsync(e=>e.Id==request.StationId&&e.ResourceKey=="stations"&&e.IsActive,ct)))return BadRequest(new{message="Cần chọn trạm sửa chữa hợp lệ."});
        var componentOnly=doc.Type is "component-receipt" or "component-proposal" or "allocation";
        if(request.Lines.Count==0||request.Lines.Any(line=>line.Quantity<=0||line.UnitPrice<0||(componentOnly?(line.ComponentId is null||line.ProductId is not null):(line.ProductId is null||line.ComponentId is not null))))return BadRequest(new{message="Dòng phiếu không hợp lệ."});
        if(request.Lines.GroupBy(line=>(line.ProductId,line.ComponentId)).Any(group=>group.Count()>1))return BadRequest(new{message="Không lặp sản phẩm/linh kiện trong phiếu."});
        foreach(var line in request.Lines){if(line.ProductId.HasValue&&!await db.Products.AnyAsync(p=>p.Id==line.ProductId,ct))return BadRequest(new{message="Không tìm thấy sản phẩm."});if(line.ComponentId.HasValue&&!await db.AdminCatalogEntries.AnyAsync(e=>e.Id==line.ComponentId&&e.ResourceKey=="components",ct))return BadRequest(new{message="Không tìm thấy linh kiện."});}
        db.StockDocumentLines.RemoveRange(doc.Lines);doc.Lines=request.Lines.Select(line=>new StockDocumentLine{ProductId=line.ProductId,ComponentId=line.ComponentId,Quantity=line.Quantity,UnitPrice=line.UnitPrice}).ToList();doc.WarehouseId=request.WarehouseId;doc.StationId=request.StationId;doc.DealerId=request.DealerId;doc.Counterparty=request.Counterparty?.Trim();doc.Notes=request.Notes?.Trim();await db.SaveChangesAsync(ct);return Ok(new{doc.Id,doc.Code,doc.Status});
    }

    [HttpPost("documents/{id:int}/confirm")]
    [ModulePermission("inventory-documents", "edit")]
    [Authorize(Roles="Admin,Manager")]
    public async Task<IActionResult> Confirm(int id,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var doc=await db.StockDocuments.Include(d=>d.Lines).FirstOrDefaultAsync(d=>d.Id==id,ct);if(doc is null)return NotFound();
        if(doc.Status!="Draft")return Conflict(new{message=$"Chỉ xác nhận phiếu nháp; trạng thái hiện tại: {doc.Status}."});
        if(doc.Type is not("receipt" or "shipment" or "component-receipt" or "allocation"))return BadRequest(new{message="Loại phiếu này không thay đổi tồn kho tại bước xác nhận."});
        foreach(var line in doc.Lines){var balance=await db.InventoryBalances.FirstOrDefaultAsync(b=>b.WarehouseId==doc.WarehouseId&&b.ProductId==line.ProductId&&b.ComponentId==line.ComponentId,ct);
            var inbound=doc.Type is "receipt" or "component-receipt";if(!inbound&&(balance is null||balance.Quantity<line.Quantity))return Conflict(new{message="Tồn kho không đủ để xuất; phiếu chưa được xác nhận."});
            if(balance is null){balance=new InventoryBalance{WarehouseId=doc.WarehouseId,ProductId=line.ProductId,ComponentId=line.ComponentId,Quantity=line.Quantity};db.InventoryBalances.Add(balance);}else{balance.Quantity+=inbound?line.Quantity:-line.Quantity;balance.UpdatedAt=DateTime.UtcNow;}
            if(doc.Type=="allocation"&&line.ComponentId.HasValue){var station=await db.StationPartBalances.FirstOrDefaultAsync(b=>b.StationId==doc.StationId&&b.ComponentId==line.ComponentId,ct);if(station is null)db.StationPartBalances.Add(new StationPartBalance{StationId=doc.StationId!.Value,ComponentId=line.ComponentId.Value,AllocatedQuantity=line.Quantity});else{station.AllocatedQuantity+=line.Quantity;station.UpdatedAt=DateTime.UtcNow;}}
        }
        doc.Status=doc.Type=="allocation"?"Allocated":"Confirmed";doc.ConfirmedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Ok(new{doc.Id,doc.Status,doc.ConfirmedAt});
    }

    [HttpPost("documents/{id:int}/approve")]
    [ModulePermission("inventory-documents", "edit")]
    [Authorize(Roles="Admin,Manager")]
    public async Task<IActionResult> Approve(int id,CancellationToken ct)=>await SetProposalStatus(id,"Approved",ct);
    [HttpPost("documents/{id:int}/reject")]
    [ModulePermission("inventory-documents", "edit")]
    [Authorize(Roles="Admin,Manager")]
    public async Task<IActionResult> Reject(int id,CancellationToken ct)=>await SetProposalStatus(id,"Rejected",ct);
    private async Task<IActionResult> SetProposalStatus(int id,string status,CancellationToken ct){var doc=await db.StockDocuments.FirstOrDefaultAsync(d=>d.Id==id,ct);if(doc is null)return NotFound();if(doc.Type!="component-proposal"||doc.Status!="PendingApproval")return Conflict(new{message="Chỉ phiếu đề xuất đang chờ duyệt mới được xử lý."});doc.Status=status;doc.ConfirmedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);return Ok(new{doc.Id,doc.Status});}

    [HttpDelete("documents/{id:int}")]
    [ModulePermission("inventory-documents")]
    [Authorize(Roles="Admin,Manager")]
    public async Task<IActionResult> DeleteDraft(int id,CancellationToken ct){var doc=await db.StockDocuments.Include(d=>d.Lines).FirstOrDefaultAsync(d=>d.Id==id,ct);if(doc is null)return NotFound();if(doc.Status!="Draft")return Conflict(new{message="Chỉ xóa được phiếu nháp."});db.StockDocuments.Remove(doc);await db.SaveChangesAsync(ct);return NoContent();}
}
