using System.Data;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.DTOs;
using WebApplication1.Middleware;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/admin/products")]
[Authorize(Roles = "Admin,Manager")]
public class AdminProductsController(AppDbContext db) : ControllerBase
{
    // GET: api/admin/products
    [HttpGet]
    [ModulePermission("product")]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p =>
                p.ProductCode.Contains(search) ||
                p.ProductName.Contains(search) ||
                p.SerialNumber.Contains(search) ||
                p.Brand.Contains(search) ||
                p.Model.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.ProductCode,
                p.ProductName,
                p.Category,
                p.Brand,
                p.Model,
                p.SerialNumber,
                p.PurchaseDate,
                p.WarrantyPeriod,
                p.OwnerUserId
            })
            .ToListAsync(ct);

        return Ok(new
        {
            items,
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    // GET: api/admin/products/1
    [HttpGet("{id:int}")]
    [ModulePermission("product")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var product = await db.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id,
                p.ProductCode,
                p.ProductName,
                p.Category,
                p.Brand,
                p.Model,
                p.SerialNumber,
                p.PurchaseDate,
                p.WarrantyPeriod,
                p.OwnerUserId
            })
            .FirstOrDefaultAsync(ct);

        return product is null ? NotFound() : Ok(product);
    }

    // POST: api/admin/products/generate-code
    // Tạo mã ngẫu nhiên và lưu sản phẩm cùng mã đó.
    [HttpPost("generate-code")]
    [ModulePermission("gencode")]
    public async Task<IActionResult> GenerateWarrantyCode(
        [FromBody] GenerateWarrantyCodeRequest request,
        CancellationToken ct)
    {
        var brand = request.Brand.Trim();
        var category = request.Category.Trim();
        var serial = request.SerialNumber.Trim();

        var brandExists = await db.AdminCatalogEntries.AnyAsync(entry =>
            entry.ResourceKey == "brands" &&
            entry.Name == brand &&
            entry.IsActive, ct);

        if (!brandExists)
            return BadRequest(new { message = "Thương hiệu không tồn tại hoặc đã ngừng hoạt động." });

        var categoryExists = await db.AdminCatalogEntries.AnyAsync(entry =>
            entry.ResourceKey == "categories" &&
            entry.Name == category &&
            entry.IsActive, ct);

        if (!categoryExists)
            return BadRequest(new { message = "Phân loại không tồn tại hoặc đã ngừng hoạt động." });

        await using var transaction =
            await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        if (await db.Products.AnyAsync(product => product.SerialNumber == serial, ct))
            return Conflict(new { message = "Serial này đã được sử dụng." });

        string code;

        do
        {
            code = $"BH-{DateTime.UtcNow:yyyyMMdd}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(6))}";
        }
        while (await db.Products.AnyAsync(product => product.ProductCode == code, ct));

        var product = new Product
        {
            ProductCode = code,
            ProductName = request.ProductName.Trim(),
            Brand = brand,
            Category = category,
            Model = request.Model.Trim(),
            SerialNumber = serial,
            PurchaseDate = request.PurchaseDate,
            WarrantyPeriod = request.WarrantyPeriod
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return CreatedAtAction(
            nameof(Get),
            new { id = product.Id },
            new
            {
                product.Id,
                warrantyCode = product.ProductCode,
                product.SerialNumber,
                product.ProductName
            });
    }

    // POST: api/admin/products
    [HttpPost]
    [ModulePermission("product")]
    public async Task<IActionResult> Create(
        AdminProductRequest request,
        CancellationToken ct)
    {
        request.ProductCode = request.ProductCode.Trim();
        request.SerialNumber = request.SerialNumber.Trim();
        request.Brand = request.Brand.Trim();
        request.Category = request.Category.Trim();
        request.Model = request.Model.Trim();

        if (!await db.AdminCatalogEntries.AnyAsync(entry =>
                entry.ResourceKey == "brands" &&
                entry.Name == request.Brand &&
                entry.IsActive, ct))
        {
            return BadRequest(new
            {
                message = "Thương hiệu không tồn tại hoặc đã ngừng hoạt động."
            });
        }

        if (!await db.AdminCatalogEntries.AnyAsync(entry =>
                entry.ResourceKey == "categories" &&
                entry.Name == request.Category &&
                entry.IsActive, ct))
        {
            return BadRequest(new
            {
                message = "Phân loại sản phẩm không tồn tại hoặc đã ngừng hoạt động."
            });
        }

        await using var transaction =
            await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        if (await db.Products.AnyAsync(product =>
                product.ProductCode == request.ProductCode ||
                (!string.IsNullOrWhiteSpace(request.SerialNumber) &&
                 product.SerialNumber == request.SerialNumber), ct))
        {
            return Conflict(new
            {
                message = "Mã sản phẩm hoặc serial đã tồn tại."
            });
        }

        if (request.OwnerUserId.HasValue &&
            !await db.Users.AnyAsync(user =>
                user.Id == request.OwnerUserId &&
                (user.Role == "User" || user.Role == "Customer"), ct))
        {
            return BadRequest(new
            {
                message = "Chỉ gán sản phẩm cho tài khoản User/Customer."
            });
        }

        var product = new Product
        {
            ProductCode = request.ProductCode,
            ProductName = request.ProductName.Trim(),
            Category = request.Category,
            Brand = request.Brand,
            Model = request.Model,
            SerialNumber = request.SerialNumber,
            PurchaseDate = request.PurchaseDate,
            WarrantyPeriod = request.WarrantyPeriod,
            OwnerUserId = request.OwnerUserId
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    // PUT: api/admin/products/1
    [HttpPut("{id:int}")]
    [ModulePermission("product")]
    public async Task<IActionResult> Update(
        int id,
        AdminProductRequest request,
        CancellationToken ct)
    {
        request.ProductCode = request.ProductCode.Trim();
        request.SerialNumber = request.SerialNumber.Trim();
        request.Brand = request.Brand.Trim();
        request.Category = request.Category.Trim();
        request.Model = request.Model.Trim();

        await using var transaction =
            await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
            return NotFound();

        if (!string.Equals(product.Brand, request.Brand, StringComparison.OrdinalIgnoreCase) &&
            !await db.AdminCatalogEntries.AnyAsync(entry =>
                entry.ResourceKey == "brands" &&
                entry.Name == request.Brand &&
                entry.IsActive, ct))
        {
            return BadRequest(new
            {
                message = "Thương hiệu không tồn tại hoặc đã ngừng hoạt động."
            });
        }

        if (!string.Equals(product.Category, request.Category, StringComparison.OrdinalIgnoreCase) &&
            !await db.AdminCatalogEntries.AnyAsync(entry =>
                entry.ResourceKey == "categories" &&
                entry.Name == request.Category &&
                entry.IsActive, ct))
        {
            return BadRequest(new
            {
                message = "Phân loại sản phẩm không tồn tại hoặc đã ngừng hoạt động."
            });
        }

        var duplicate = await db.Products.AnyAsync(other =>
            other.Id != id &&
            (other.ProductCode == request.ProductCode ||
             (!string.IsNullOrWhiteSpace(request.SerialNumber) &&
              other.SerialNumber == request.SerialNumber)), ct);

        if (duplicate)
            return Conflict(new { message = "Mã sản phẩm hoặc serial đã tồn tại." });

        if (request.OwnerUserId.HasValue &&
            !await db.Users.AnyAsync(user =>
                user.Id == request.OwnerUserId &&
                (user.Role == "User" || user.Role == "Customer"), ct))
        {
            return BadRequest(new
            {
                message = "Chỉ gán sản phẩm cho tài khoản User/Customer."
            });
        }

        product.ProductCode = request.ProductCode;
        product.ProductName = request.ProductName.Trim();
        product.Category = request.Category;
        product.Brand = request.Brand;
        product.Model = request.Model;
        product.SerialNumber = request.SerialNumber;
        product.PurchaseDate = request.PurchaseDate;
        product.WarrantyPeriod = request.WarrantyPeriod;
        product.OwnerUserId = request.OwnerUserId;

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return NoContent();
    }

    // DELETE: api/admin/products/1
    [HttpDelete("{id:int}")]
    [ModulePermission("product")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
            return NotFound();

        var inUse =
            await db.InventoryBalances.AnyAsync(b => b.ProductId == id, ct) ||
            await db.StockDocumentLines.AnyAsync(line => line.ProductId == id, ct) ||
            await db.WarrantyRequests.AnyAsync(request => request.ProductId == id, ct);

        if (inUse)
        {
            return Conflict(new
            {
                message = "Sản phẩm có lịch sử kho hoặc bảo hành; không thể xóa."
            });
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }
}