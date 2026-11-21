using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.DTOs;
using WebApplication1.Models;
using WebApplication1.Middleware;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/admin/products")]
[Authorize(Roles = "Admin,Manager")]
public class AdminProductsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [ModulePermission("product")]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] string? category, [FromQuery] int page=1, [FromQuery] int pageSize=20, CancellationToken ct=default)
    {
        page=Math.Max(1,page); pageSize=Math.Clamp(pageSize,1,100);
        var query=db.Products.AsNoTracking().AsQueryable();
        if(!string.IsNullOrWhiteSpace(search)) query=query.Where(p=>p.ProductCode.Contains(search)||p.ProductName.Contains(search)||p.SerialNumber.Contains(search)||p.Brand.Contains(search)||p.Model.Contains(search));
        if(!string.IsNullOrWhiteSpace(category)) query=query.Where(p=>p.Category==category);
        var total=await query.CountAsync(ct);
        var items=await query.OrderByDescending(p=>p.Id).Skip((page-1)*pageSize).Take(pageSize)
            .Select(p=>new {p.Id,p.ProductCode,p.ProductName,p.Category,p.Brand,p.Model,p.SerialNumber,p.PurchaseDate,p.WarrantyPeriod,p.OwnerUserId}).ToListAsync(ct);
        return Ok(new{items,total,page,pageSize,totalPages=(int)Math.Ceiling(total/(double)pageSize)});
    }

    [HttpGet("{id:int}")]
    [ModulePermission("product")]
    public async Task<IActionResult> Get(int id,CancellationToken ct){var p=await db.Products.AsNoTracking().Where(x=>x.Id==id).Select(x=>new{x.Id,x.ProductCode,x.ProductName,x.Category,x.Brand,x.Model,x.SerialNumber,x.PurchaseDate,x.WarrantyPeriod,x.OwnerUserId}).FirstOrDefaultAsync(ct);return p is null?NotFound():Ok(p);}

    [HttpPost]
    [ModulePermission("product")]
    public async Task<IActionResult> Create(AdminProductRequest request,CancellationToken ct)
    {
        request.ProductCode=request.ProductCode.Trim();request.SerialNumber=request.SerialNumber.Trim();request.Brand=request.Brand.Trim();request.Category=request.Category.Trim();request.Model=request.Model.Trim();
        if(!await db.AdminCatalogEntries.AnyAsync(entry=>entry.ResourceKey=="brands"&&entry.Name==request.Brand&&entry.IsActive,ct))return BadRequest(new{message="Thương hiệu không tồn tại hoặc đã ngừng hoạt động."});
        if(!await db.AdminCatalogEntries.AnyAsync(entry=>entry.ResourceKey=="categories"&&entry.Name==request.Category&&entry.IsActive,ct))return BadRequest(new{message="Phân loại sản phẩm không tồn tại hoặc đã ngừng hoạt động."});
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        if(await db.Products.AnyAsync(p=>p.ProductCode==request.ProductCode||(!string.IsNullOrWhiteSpace(request.SerialNumber)&&p.SerialNumber==request.SerialNumber),ct))return Conflict(new{message="Mã sản phẩm hoặc serial đã tồn tại."});
        if(request.OwnerUserId.HasValue&&!await db.Users.AnyAsync(u=>u.Id==request.OwnerUserId&&(u.Role=="User"||u.Role=="Customer"),ct))return BadRequest(new{message="Chỉ gán sản phẩm cho tài khoản User/Customer."});
        var p=new Product{ProductCode=request.ProductCode.Trim(),ProductName=request.ProductName.Trim(),Category=request.Category.Trim(),Brand=request.Brand.Trim(),Model=request.Model.Trim(),SerialNumber=request.SerialNumber.Trim(),PurchaseDate=request.PurchaseDate,WarrantyPeriod=request.WarrantyPeriod,OwnerUserId=request.OwnerUserId};
        db.Products.Add(p);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return CreatedAtAction(nameof(Get),new{id=p.Id},p);
    }

    [HttpPut("{id:int}")]
    [ModulePermission("product")]
    public async Task<IActionResult> Update(int id,AdminProductRequest request,CancellationToken ct)
    {
        request.ProductCode=request.ProductCode.Trim();request.SerialNumber=request.SerialNumber.Trim();request.Brand=request.Brand.Trim();request.Category=request.Category.Trim();request.Model=request.Model.Trim();
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var p=await db.Products.FirstOrDefaultAsync(x=>x.Id==id,ct);if(p is null)return NotFound();
        // Existing products may predate catalog validation. Preserve unchanged legacy values,
        // while requiring active catalog entries whenever either reference is changed.
        if(!string.Equals(p.Brand,request.Brand,StringComparison.OrdinalIgnoreCase)&&!await db.AdminCatalogEntries.AnyAsync(entry=>entry.ResourceKey=="brands"&&entry.Name==request.Brand&&entry.IsActive,ct))return BadRequest(new{message="Thương hiệu không tồn tại hoặc đã ngừng hoạt động."});
        if(!string.Equals(p.Category,request.Category,StringComparison.OrdinalIgnoreCase)&&!await db.AdminCatalogEntries.AnyAsync(entry=>entry.ResourceKey=="categories"&&entry.Name==request.Category&&entry.IsActive,ct))return BadRequest(new{message="Phân loại sản phẩm không tồn tại hoặc đã ngừng hoạt động."});
        if(await db.Products.AnyAsync(x=>x.Id!=id&&(x.ProductCode==request.ProductCode||(!string.IsNullOrWhiteSpace(request.SerialNumber)&&x.SerialNumber==request.SerialNumber)),ct))return Conflict(new{message="Mã sản phẩm hoặc serial đã tồn tại."});
        if(request.OwnerUserId.HasValue&&!await db.Users.AnyAsync(u=>u.Id==request.OwnerUserId&&(u.Role=="User"||u.Role=="Customer"),ct))return BadRequest(new{message="Chỉ gán sản phẩm cho tài khoản User/Customer."});
        p.ProductCode=request.ProductCode.Trim();p.ProductName=request.ProductName.Trim();p.Category=request.Category.Trim();p.Brand=request.Brand.Trim();p.Model=request.Model.Trim();p.SerialNumber=request.SerialNumber.Trim();p.PurchaseDate=request.PurchaseDate;p.WarrantyPeriod=request.WarrantyPeriod;p.OwnerUserId=request.OwnerUserId;
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ModulePermission("product")]
    public async Task<IActionResult> Delete(int id,CancellationToken ct){var p=await db.Products.FirstOrDefaultAsync(x=>x.Id==id,ct);if(p is null)return NotFound();if(await db.InventoryBalances.AnyAsync(b=>b.ProductId==id,ct)||await db.StockDocumentLines.AnyAsync(l=>l.ProductId==id,ct)||await db.WarrantyRequests.AnyAsync(r=>r.ProductId==id,ct))return Conflict(new{message="Sản phẩm có lịch sử kho hoặc bảo hành; không thể xóa."});db.Products.Remove(p);await db.SaveChangesAsync(ct);return NoContent();}
}
