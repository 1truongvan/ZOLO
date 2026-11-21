using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;

namespace WebApplication1.Services;

public interface IModulePermissionService
{
    Task<bool> IsAllowedAsync(ClaimsPrincipal principal, HttpContext context, string moduleKey, string? requestedOperation, CancellationToken ct);
}

public sealed class ModulePermissionService(AppDbContext db) : IModulePermissionService
{
    private static readonly Dictionary<string, string> CatalogModules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["brands"] = "trademark", ["categories"] = "productcategory", ["warehouses"] = "warehouse",
        ["warranty-types"] = "warranticategory", ["fault-groups"] = "warrantitype", ["service-prices"] = "servicecost",
        ["travel-prices"] = "movingcost", ["components"] = "accessarycenter", ["stations"] = "warranti", ["dealers"] = "warehouse"
    };

    public async Task<bool> IsAllowedAsync(ClaimsPrincipal principal, HttpContext context, string moduleKey, string? requestedOperation, CancellationToken ct)
    {
        var role = principal.FindFirstValue(ClaimTypes.Role);
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.IsNullOrWhiteSpace(role)) return false;
        if (role.Equals("Customer", StringComparison.OrdinalIgnoreCase)) role = "User";

        var method = context.Request.Method.ToUpperInvariant();
        var operation = requestedOperation ?? method switch
        {
            "GET" or "HEAD" => "view", "POST" => "create", "PUT" or "PATCH" => "edit", "DELETE" => "delete", _ => "deny"
        };
        var colon = moduleKey.IndexOf(':');
        string? detailKey = colon >= 0 ? moduleKey[(colon + 1)..] : null;
        var key = colon >= 0 ? moduleKey[..colon] : moduleKey;
        if (moduleKey == "catalog")
        {
            var resource = Convert.ToString(context.Request.RouteValues["resource"]);
            if (resource is null || !CatalogModules.TryGetValue(resource, out key!)) return false;
            if (resource.Equals("stations", StringComparison.OrdinalIgnoreCase)) detailKey = "manage-stations";
        }
        else if (moduleKey == "inventory-balances")
        {
            key = context.Request.Path.Value?.EndsWith("station-parts", StringComparison.OrdinalIgnoreCase) == true ? "accessarycenter" : "warehouse";
            detailKey = key == "warehouse" ? "stock-report" : "track-stock";
        }
        else if (moduleKey == "inventory-documents")
        {
            var type = context.Request.Query["type"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(type) && method == "POST")
            {
                context.Request.EnableBuffering();
                try
                {
                    using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: ct);
                    if (document.RootElement.TryGetProperty("type", out var typeValue)) type = typeValue.GetString();
                }
                catch (JsonException) { }
                finally { if (context.Request.Body.CanSeek) context.Request.Body.Position = 0; }
            }
            if (string.IsNullOrWhiteSpace(type) && context.Request.RouteValues.TryGetValue("id", out var idValue)
                && int.TryParse(Convert.ToString(idValue), out var documentId))
                type = await db.StockDocuments.AsNoTracking().Where(d => d.Id == documentId).Select(d => d.Type).FirstOrDefaultAsync(ct);
            key = type?.ToLowerInvariant() switch
            {
                "receipt" => "import", "shipment" => "export", "component-receipt" => "accessaryimport",
                "component-proposal" or "allocation" => "accessaryexport", _ => ""
            };
            if (key.Length == 0) return false;
            if (context.Request.Path.Value?.EndsWith("/confirm", StringComparison.OrdinalIgnoreCase) == true)
                detailKey = key == "import" ? "confirm-receipt" : key == "export" ? "confirm-shipment" : "confirm";
        }

        var permission = await db.RoleModulePermissions.AsNoTracking()
            .Where(p => p.RoleName == role && p.Module.ModuleKey == key)
            .Select(p => new { p.MenuVisible, p.CanView, p.CanCreate, p.CanEdit, p.CanDelete }).FirstOrDefaultAsync(ct);
        if (permission is null) return false;
        var allowed = operation switch
        {
            "view" => permission.CanView,
            "create" => permission.CanCreate,
            "edit" => permission.CanEdit,
            "delete" => permission.CanDelete,
            _ => false
        };
        if (!allowed) return false;
        if (detailKey is null) return true;
        return await db.RolePermissionDetails.AsNoTracking().AnyAsync(d => d.RolePermission.RoleName == role
            && d.RolePermission.Module.ModuleKey == key && d.PermissionDetail.PermissionKey == detailKey && d.IsGranted, ct);
    }
}
