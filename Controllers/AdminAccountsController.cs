using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.DTOs;
using WebApplication1.Models;
using WebApplication1.Middleware;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/admin/accounts")]
[Authorize]
public class AdminAccountsController(AppDbContext db) : ControllerBase
{
    private static readonly string[] AllowedRoles = ["User", "Customer", "Admin", "Manager", "Technician"];

    [HttpGet]
    [ModulePermission("customer", "view")]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] string? role, [FromQuery] bool? active,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Users.AsNoTracking().AsQueryable();
        if (!IsAdmin()) query = query.Where(user => user.Role == "User" || user.Role == "Customer");
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(user => user.Username.Contains(search) || user.Email.Contains(search)
            || (user.FullName != null && user.FullName.Contains(search)) || (user.PhoneNumber != null && user.PhoneNumber.Contains(search)));
        if (!string.IsNullOrWhiteSpace(role)) query = query.Where(user => user.Role == role);
        if (active.HasValue) query = query.Where(user => user.IsActive == active.Value);
        var total = await query.CountAsync(ct);
        var users = await query.OrderByDescending(user => user.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(user => new { user.Id, user.Username, user.FullName, user.Email, user.PhoneNumber, user.CreatedAt, user.IsActive, user.Role,
                ProductCount = db.Products.Count(product => product.OwnerUserId == user.Id) })
            .ToListAsync(ct);
        return Ok(new { items = users, total, page, pageSize, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    [HttpGet("{id:int}")]
    [ModulePermission("user", "view")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking().Where(user => user.Id == id);
        if (!IsAdmin()) query = query.Where(user => user.Role == "User" || user.Role == "Customer");
        var user = await query
            .Select(user => new { user.Id, user.Username, user.FullName, user.Email, user.PhoneNumber, user.CreatedAt, user.IsActive, user.Role,
                Products = db.Products.Where(product => product.OwnerUserId == user.Id)
                    .Select(product => new { product.Id, product.ProductCode, product.ProductName, product.SerialNumber, product.WarrantyPeriod }).ToList() })
            .FirstOrDefaultAsync(ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    [ModulePermission("user", "create")]
    public async Task<IActionResult> Create(AdminAccountRequest request, CancellationToken ct)
    {
        var role = NormalizeRole(request.Role);
        if (!IsAdmin() && role is not ("User" or "Customer")) return Forbid();
        if (role is null) return BadRequest(new { message = "Vai trò không hợp lệ." });
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 12)
            return BadRequest(new { message = "Mật khẩu khởi tạo cần ít nhất 12 ký tự." });
        if (await db.Users.AnyAsync(user => user.Username == request.Username || user.Email == request.Email, ct))
            return Conflict(new { message = "Username hoặc email đã tồn tại." });
        HashPassword(request.Password, out var hash, out var salt);
        var user = new User { Username = request.Username.Trim(), Email = request.Email.Trim(), FullName = request.FullName?.Trim(), PhoneNumber = request.PhoneNumber?.Trim(), Role = role, IsActive = request.IsActive, PasswordHash = hash, PasswordSalt = salt };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, new { user.Id, user.Username, user.FullName, user.Email, user.PhoneNumber, user.Role, user.IsActive, user.CreatedAt });
    }

    [HttpPut("{id:int}")]
    [ModulePermission("user", "edit")]
    public async Task<IActionResult> Update(int id, AdminAccountRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (user is null) return NotFound();
        if (!IsAdmin() && user.Role is not ("User" or "Customer")) return Forbid();
        var role = NormalizeRole(request.Role);
        if (!IsAdmin() && role is not ("User" or "Customer")) return Forbid();
        if (role is null) return BadRequest(new { message = "Vai trò không hợp lệ." });
        if (await db.Users.AnyAsync(item => item.Id != id && (item.Username == request.Username || item.Email == request.Email), ct))
            return Conflict(new { message = "Username hoặc email đã được tài khoản khác sử dụng." });
        if (user.Id == CurrentUserId() && !request.IsActive) return BadRequest(new { message = "Không thể khóa tài khoản đang đăng nhập." });
        user.Username = request.Username.Trim(); user.Email = request.Email.Trim(); user.FullName = request.FullName?.Trim();
        user.PhoneNumber = request.PhoneNumber?.Trim(); user.Role = role; user.IsActive = request.IsActive;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (request.Password.Length < 12) return BadRequest(new { message = "Mật khẩu cần ít nhất 12 ký tự." });
            HashPassword(request.Password, out var hash, out var salt); user.PasswordHash = hash; user.PasswordSalt = salt;
        }
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:int}/status")]
    [ModulePermission("user", "edit")]
    public async Task<IActionResult> SetStatus(int id, [FromBody] AccountStatusRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (user is null) return NotFound();
        if (!IsAdmin() && user.Role is not ("User" or "Customer")) return Forbid();
        if (user.Id == CurrentUserId() && !request.IsActive) return BadRequest(new { message = "Không thể khóa tài khoản đang đăng nhập." });
        user.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return Ok(new { user.Id, user.IsActive });
    }

    [HttpDelete("{id:int}")]
    [ModulePermission("user", "delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (user is null) return NotFound();
        if (!IsAdmin() && user.Role is not ("User" or "Customer")) return Forbid();
        if (user.Id == CurrentUserId()) return BadRequest(new { message = "Không thể xóa tài khoản đang đăng nhập; hãy khóa tài khoản." });
        if (await db.Products.AnyAsync(product => product.OwnerUserId == id, ct))
            return Conflict(new { message = "Tài khoản đang sở hữu sản phẩm; hãy khóa thay vì xóa để giữ lịch sử." });
        if (await db.WarrantyRequests.AnyAsync(request => request.CustomerUserId == id || request.AssignedTechnicianId == id, ct))
            return Conflict(new { message = "Tài khoản có lịch sử yêu cầu bảo hành; hãy khóa thay vì xóa." });
        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private int CurrentUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(id, out var parsed) ? parsed : -1;
    }

    private bool IsAdmin() => string.Equals(User.FindFirstValue(ClaimTypes.Role), "Admin", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeRole(string role) => AllowedRoles.FirstOrDefault(allowed => string.Equals(allowed, role.Trim(), StringComparison.OrdinalIgnoreCase));

    private static void HashPassword(string password, out byte[] hash, out byte[] salt)
    {
        using var hmac = new HMACSHA512(); salt = hmac.Key; hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
    }
}

public sealed class AccountStatusRequest { public bool IsActive { get; set; } }
