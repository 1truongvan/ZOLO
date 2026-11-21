using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Middleware;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/products")]
[Authorize(Roles = "Customer,User")]
public sealed class CustomerProductsController(AppDbContext db) : ControllerBase
{
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : -1;

    [HttpGet("mine")]
    [ModulePermission("product", "view")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var items = await db.Products.AsNoTracking()
            .Where(product => product.OwnerUserId == UserId)
            .OrderByDescending(product => product.Id)
            .Select(product => new
            {
                product.Id,
                product.ProductCode,
                product.ProductName,
                product.Category,
                product.Brand,
                product.Model,
                product.SerialNumber,
                product.PurchaseDate,
                product.WarrantyPeriod,
                WarrantyExpiresAt = product.PurchaseDate.AddMonths(product.WarrantyPeriod),
                IsUnderWarranty = product.WarrantyPeriod > 0 && product.PurchaseDate.AddMonths(product.WarrantyPeriod) >= now
            })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost("register")]
    [ModulePermission("product", "create")]
    public async Task<IActionResult> Register(RegisterProductRequest request, CancellationToken ct)
    {
        var code = request.ProductCode.Trim();
        var serial = request.SerialNumber.Trim();
        var matches = await db.Products.AsNoTracking()
            .Where(item => item.ProductCode == code && item.SerialNumber == serial)
            .Select(item => new { item.Id, item.OwnerUserId })
            .Take(2)
            .ToListAsync(ct);

        if (matches.Count == 0)
            return NotFound(new { message = "Không tìm thấy sản phẩm phù hợp với mã và số serial đã nhập." });
        if (matches.Count > 1)
            return Conflict(new { message = "Có nhiều sản phẩm trùng mã và serial; quản trị viên cần kiểm tra dữ liệu trước khi đăng ký." });

        var product = matches[0];
        if (product.OwnerUserId.HasValue)
            return Conflict(new { message = product.OwnerUserId == UserId ? "Sản phẩm đã được đăng ký vào tài khoản của bạn." : "Sản phẩm đã được đăng ký bởi một tài khoản khác." });

        // Atomic claim prevents two accounts registering the same product concurrently.
        var claimed = await db.Products
            .Where(item => item.Id == product.Id && item.OwnerUserId == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.OwnerUserId, UserId), ct);
        if (claimed == 0)
            return Conflict(new { message = "Sản phẩm vừa được đăng ký bởi tài khoản khác. Hãy tải lại danh sách." });

        return Ok(new { message = "Đăng ký sản phẩm thành công.", productId = product.Id });
    }
}

public sealed class RegisterProductRequest
{
    [Required, StringLength(60, MinimumLength = 1)]
    public string ProductCode { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 1)]
    public string SerialNumber { get; set; } = string.Empty;
}
