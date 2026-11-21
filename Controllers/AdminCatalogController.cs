using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.DTOs;
using WebApplication1.Models;
using WebApplication1.Middleware;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/admin/catalog")]
[Authorize]
public class AdminCatalogController(AppDbContext db) : ControllerBase
{
    private static readonly HashSet<string> Resources = new(StringComparer.OrdinalIgnoreCase)
    {
        "brands", "categories", "warehouses", "warranty-types", "fault-groups",
        "service-prices", "travel-prices", "components", "stations", "dealers"
    };

    [HttpGet("{resource}")]
    [ModulePermission("catalog")]
    public async Task<IActionResult> List(string resource, [FromQuery] string? search, [FromQuery] bool? active,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var key = Normalize(resource);
        if (key is null) return NotFound(new { message = "Không có phân hệ danh mục này." });
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.AdminCatalogEntries.AsNoTracking().Where(item => item.ResourceKey == key);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(item => item.Code.Contains(search) || item.Name.Contains(search) || (item.Description != null && item.Description.Contains(search)));
        if (active.HasValue) query = query.Where(item => item.IsActive == active.Value);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(item => item.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new { items, total, page, pageSize, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    [HttpGet("{resource}/{id:int}")]
    [ModulePermission("catalog")]
    public async Task<IActionResult> Get(string resource, int id, CancellationToken ct)
    {
        var key = Normalize(resource);
        if (key is null) return NotFound();
        var item = await db.AdminCatalogEntries.AsNoTracking().FirstOrDefaultAsync(entry => entry.Id == id && entry.ResourceKey == key, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{resource}")]
    [ModulePermission("catalog")]
    public async Task<IActionResult> Create(string resource, AdminCatalogRequest request, CancellationToken ct)
    {
        var key = Normalize(resource);
        if (key is null) return NotFound();
        request.ResponsibleUserId ??= request.ManagerUserId;
        request.ContactPhone ??= request.PhoneNumber;
        var validation = Validate(key, request);
        if (validation is not null) return BadRequest(new { message = validation });
        if (key == "warehouses" && request.ResponsibleUserId.HasValue && !await db.Users.AnyAsync(user => user.Id == request.ResponsibleUserId && user.Role == "Manager" && user.IsActive, ct)) return BadRequest(new { message = "Người quản lý kho phải là tài khoản Manager đang hoạt động." });
        var code = request.Code.Trim();
        var uniqueName = key is "brands" or "categories" or "warranty-types" or "fault-groups" or "warehouses" or "components";
        if (await db.AdminCatalogEntries.AnyAsync(entry => entry.ResourceKey == key && (entry.Code == code || (uniqueName && entry.Name == request.Name.Trim())), ct))
            return Conflict(new { message = "Mã hoặc tên đã tồn tại trong phân hệ này." });
        var item = Map(key, request);
        db.AdminCatalogEntries.Add(item);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { resource, id = item.Id }, item);
    }

    [HttpPut("{resource}/{id:int}")]
    [ModulePermission("catalog")]
    public async Task<IActionResult> Update(string resource, int id, AdminCatalogRequest request, CancellationToken ct)
    {
        var key = Normalize(resource);
        if (key is null) return NotFound();
        request.ResponsibleUserId ??= request.ManagerUserId;
        request.ContactPhone ??= request.PhoneNumber;
        var validation = Validate(key, request);
        if (validation is not null) return BadRequest(new { message = validation });
        if (key == "warehouses" && request.ResponsibleUserId.HasValue && !await db.Users.AnyAsync(user => user.Id == request.ResponsibleUserId && user.Role == "Manager" && user.IsActive, ct)) return BadRequest(new { message = "Người quản lý kho phải là tài khoản Manager đang hoạt động." });
        var item = await db.AdminCatalogEntries.FirstOrDefaultAsync(entry => entry.Id == id && entry.ResourceKey == key, ct);
        if (item is null) return NotFound();
        var code = request.Code.Trim();
        var uniqueName = key is "brands" or "categories" or "warranty-types" or "fault-groups" or "warehouses" or "components";
        if (await db.AdminCatalogEntries.AnyAsync(entry => entry.ResourceKey == key && entry.Id != id && (entry.Code == code || (uniqueName && entry.Name == request.Name.Trim())), ct))
            return Conflict(new { message = "Mã hoặc tên đã tồn tại trong phân hệ này." });
        Apply(item, request);
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(item);
    }

    [HttpDelete("{resource}/{id:int}")]
    [ModulePermission("catalog")]
    public async Task<IActionResult> Delete(string resource, int id, CancellationToken ct)
    {
        var key = Normalize(resource);
        if (key is null) return NotFound();
        var item = await db.AdminCatalogEntries.FirstOrDefaultAsync(entry => entry.Id == id && entry.ResourceKey == key, ct);
        if (item is null) return NotFound();
        var inUse = key switch
        {
            "brands" => await db.Products.AnyAsync(p => p.Brand == item.Name, ct),
            "categories" => await db.Products.AnyAsync(p => p.Category == item.Name, ct)
                || await db.AdminCatalogEntries.AnyAsync(entry => entry.ResourceKey == "service-prices" && entry.ProductGroup == item.Name, ct),
            "fault-groups" => await db.AdminCatalogEntries.AnyAsync(entry => entry.ResourceKey == "service-prices" && entry.FaultGroup == item.Name, ct),
            "warehouses" => await db.InventoryBalances.AnyAsync(b => b.WarehouseId == id, ct) || await db.StockDocuments.AnyAsync(d => d.WarehouseId == id, ct),
            "components" => await db.InventoryBalances.AnyAsync(b => b.ComponentId == id, ct) || await db.StockDocumentLines.AnyAsync(line => line.ComponentId == id, ct) || await db.StationPartBalances.AnyAsync(b => b.ComponentId == id, ct),
            "stations" => await db.StationPartBalances.AnyAsync(b => b.StationId == id, ct) || await db.StockDocuments.AnyAsync(d => d.StationId == id, ct),
            "dealers" => await db.StockDocuments.AnyAsync(d => d.DealerId == id, ct),
            _ => false
        };
        if (inUse) return Conflict(new { message = "Danh mục đang được sử dụng; hãy chuyển sang ngừng hoạt động thay vì xóa." });
        db.AdminCatalogEntries.Remove(item);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static string? Normalize(string resource)
    {
        var key = resource.Trim().ToLowerInvariant();
        return Resources.Contains(key) ? key : null;
    }

    private static string? Validate(string key, AdminCatalogRequest request)
    {
        if (request.RangeTo.HasValue && request.RangeFrom.HasValue && request.RangeTo < request.RangeFrom)
            return "Phạm vi kết thúc phải lớn hơn hoặc bằng phạm vi bắt đầu.";
        if (request.StartsAt.HasValue && request.EndsAt.HasValue && request.EndsAt < request.StartsAt)
            return "Ngày kết thúc phải sau ngày bắt đầu.";
        if (key is "service-prices" or "travel-prices" && request.Amount is null)
            return "Vui lòng nhập giá áp dụng.";
        if (key == "travel-prices" && string.IsNullOrWhiteSpace(request.Region))
            return "Vui lòng nhập khu vực áp dụng.";
        if (key == "warranty-types" && request.DurationMonths is < 0)
            return "Thời hạn bảo hành không hợp lệ.";
        return null;
    }

    private static AdminCatalogEntry Map(string key, AdminCatalogRequest request) => new AdminCatalogEntry { ResourceKey = key, Code = request.Code.Trim(), Name = request.Name.Trim(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }.Also(item => Apply(item, request));

    private static void Apply(AdminCatalogEntry item, AdminCatalogRequest request)
    {
        item.Code = request.Code.Trim(); item.Name = request.Name.Trim(); item.Description = request.Description?.Trim();
        item.LogoUrl = request.LogoUrl?.Trim(); item.Address = request.Address?.Trim(); item.ContactPhone = request.ContactPhone?.Trim(); item.ResponsibleUserId = request.ResponsibleUserId; item.ProductGroup = request.ProductGroup?.Trim();
        item.FaultGroup = request.FaultGroup?.Trim(); item.Region = request.Region?.Trim(); item.Unit = request.Unit?.Trim();
        item.Amount = request.Amount; item.RangeFrom = request.RangeFrom; item.RangeTo = request.RangeTo;
        item.DurationMonths = request.DurationMonths; item.StartsAt = request.StartsAt; item.EndsAt = request.EndsAt; item.IsActive = request.IsActive;
    }
}

internal static class CatalogMappingExtensions
{
    public static T Also<T>(this T value, Action<T> action) { action(value); return value; }
}
