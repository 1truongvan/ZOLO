using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/admin/permissions")]
[Authorize]
public sealed class AdminPermissionsController(AppDbContext db) : ControllerBase
{
    private static readonly string[] AllowedRoles = ["Admin", "Manager", "Technician", "User"];

    [HttpGet("{role}")]
    public async Task<IActionResult> Get(string role, CancellationToken ct)
    {
        if (!AllowedRoles.Contains(role, StringComparer.Ordinal)) return BadRequest(new { message = "Vai trò không hợp lệ." });
        var actualRole = User.FindFirstValue(ClaimTypes.Role);
        if (!string.Equals(actualRole, "Admin", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(string.Equals(actualRole, "Customer", StringComparison.OrdinalIgnoreCase) ? "User" : actualRole, role, StringComparison.OrdinalIgnoreCase))
            return Forbid();
        var modules = await db.PermissionModules.AsNoTracking().OrderBy(m => m.GroupKey).ThenBy(m => m.SortOrder)
            .Select(m => new { m.Id, m.ModuleKey, m.GroupKey, m.Name, m.SortOrder,
                Details = m.Details.OrderBy(d => d.SortOrder).Select(d => new { d.PermissionKey, d.Name }),
                Permission = m.RolePermissions.Where(p => p.RoleName == role).Select(p => new
                { p.MenuVisible, p.CanView, p.CanCreate, p.CanEdit, p.CanDelete,
                    Details = p.Details.Select(d => new { d.PermissionDetail.PermissionKey, d.IsGranted }) }).FirstOrDefault() })
            .ToListAsync(ct);
        return Ok(new { role, modules });
    }

    [HttpPut("{role}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Save(string role, [FromBody] SaveRolePermissions request, CancellationToken ct)
    {
        if (!AllowedRoles.Contains(role, StringComparer.Ordinal)) return BadRequest(new { message = "Vai trò không hợp lệ." });
        if (role == "Admin") return Conflict(new { message = "Quyền Admin là quyền hệ thống và không thể chỉnh sửa." });
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? updatedBy = int.TryParse(userIdValue, out var id) ? id : null;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var moduleIds = request.Modules.Select(m => m.ModuleId).Distinct().ToArray();
        var permissions = await db.RoleModulePermissions.Include(p => p.Details)
            .Where(p => p.RoleName == role && moduleIds.Contains(p.PermissionModuleId)).ToListAsync(ct);
        if (permissions.Count != moduleIds.Length) return BadRequest(new { message = "Một hoặc nhiều module không tồn tại." });
        var detailLookup = await db.PermissionDetails.Where(d => moduleIds.Contains(d.PermissionModuleId))
            .ToDictionaryAsync(d => (d.PermissionModuleId, d.PermissionKey), ct);
        foreach (var incoming in request.Modules)
        {
            var permission = permissions.Single(p => p.PermissionModuleId == incoming.ModuleId);
            permission.MenuVisible = incoming.MenuVisible;
            permission.CanView = incoming.CanView;
            permission.CanCreate = incoming.CanCreate;
            permission.CanEdit = incoming.CanEdit;
            permission.CanDelete = incoming.CanDelete;
            permission.UpdatedAt = DateTime.UtcNow;
            permission.UpdatedByUserId = updatedBy;
            foreach (var (key, granted) in incoming.Details)
            {
                if (!detailLookup.TryGetValue((incoming.ModuleId, key), out var definition))
                    return BadRequest(new { message = $"Quyền chi tiết không hợp lệ: {key}." });
                var detail = permission.Details.FirstOrDefault(d => d.PermissionDetailId == definition.Id);
                if (detail is null) permission.Details.Add(new RolePermissionDetail { PermissionDetailId = definition.Id, IsGranted = granted });
                else detail.IsGranted = granted;
            }
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }
}

public sealed record SaveRolePermissions(List<SaveModulePermission> Modules);
public sealed record SaveModulePermission(int ModuleId, bool MenuVisible, bool CanView, bool CanCreate, bool CanEdit, bool CanDelete, Dictionary<string, bool> Details);
