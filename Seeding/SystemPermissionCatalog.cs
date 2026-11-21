using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Seeding;

public static class SystemPermissionCatalog
{
    private static readonly (string Group, string[] Modules)[] Catalog =
    [
        ("warranty-config", ["movingcost:Giá di chuyển:region,distance", "servicecost:Giá dịch vụ:product-fault", "warranti:Phiếu bảo hành:approve,close,print,propose-parts,manage-stations,manage-technicians,completed", "warranticategory:Phân loại bảo hành:duration", "warrantitype:Nhóm lỗi:product-group"]),
        ("customer-care", ["customer:Khách hàng:history,notify", "maintenance:Bảo trì:schedule,remind"]),
        ("product-codes", ["gencode:Tạo mã:generate,print", "model:Model:link-product", "product:Mã bảo hành:lookup", "productcategory:Phân loại hàng:link-product", "trademark:Thương hiệu:logo,company-info"]),
        ("warehouse", ["warehouse:Kho hàng:manage,stock-report,receive,ship,confirm-receipt,confirm-shipment"]),
        ("components", ["accessary:Linh kiện:track-stock,low-stock,audit,report", "accessarycenter:Linh kiện tổng:track-stock,low-stock,audit,report", "accessaryexport:Xuất linh kiện:export,allocate", "accessaryimport:Nhập linh kiện:import,confirm"]),
        ("messaging", ["sms:SMS:send,templates", "zns:ZNS:send,template"]),
        ("system-accounts", ["role:Vai trò:manage,permissions", "setting:Cài đặt:configure", "user:Tài khoản:manage"]),
        ("extensions", ["api:API:manage,limits", "article:Bài viết:manage", "dashboard:Dashboard:view", "errorcode:Mã lỗi:manage", "export:Xuất kho:internal", "giftcustomer:Quà tặng khách hàng:give", "giftuser:Quà tặng người dùng:give", "import:Nhập hàng:receive", "module:Module:manage", "multimessage:Gửi tin đa kênh:send", "price:Giá:manage", "topup:Nạp tiền:manage", "warranticate:Phân loại phiếu:manage", "zone:Khu vực:manage"])
    ];

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        var defaultRoles = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["movingcost"] = ["Admin", "Manager"], ["servicecost"] = ["Admin", "Manager"],
            ["warranti"] = ["Admin", "Manager", "Technician", "User"], ["warranticategory"] = ["Admin", "Manager"],
            ["warrantitype"] = ["Admin", "Manager"], ["customer"] = ["Admin", "Manager"], ["maintenance"] = ["Admin", "Manager"],
            ["gencode"] = ["Admin"], ["model"] = ["Admin", "Manager"], ["product"] = ["Admin", "Manager", "User"],
            ["productcategory"] = ["Admin", "Manager"], ["trademark"] = ["Admin", "Manager"], ["warehouse"] = ["Admin", "Manager"],
            ["accessary"] = ["Admin", "Manager", "Technician"], ["accessarycenter"] = ["Admin", "Manager"],
            ["accessaryexport"] = ["Admin", "Manager", "Technician"], ["accessaryimport"] = ["Admin", "Manager"],
            ["dashboard"] = ["Admin", "Manager"]
        };
        var roleNames = new[] { "Admin", "Manager", "Technician", "User" };
        var groupOrder = 0;
        foreach (var (group, definitions) in Catalog)
        {
            groupOrder++;
            var moduleOrder = 0;
            foreach (var item in definitions)
            {
                var parts = item.Split(':');
                var key = parts[0];
                var module = await db.PermissionModules.Include(m => m.Details).FirstOrDefaultAsync(m => m.ModuleKey == key, ct);
                if (module is null)
                {
                    module = new PermissionModule { ModuleKey = key, GroupKey = group, Name = parts[1], SortOrder = ++moduleOrder };
                    db.PermissionModules.Add(module);
                    await db.SaveChangesAsync(ct);
                }
                var details = parts.Length < 3 || string.IsNullOrWhiteSpace(parts[2]) ? [] : parts[2].Split(',');
                foreach (var detailKey in details)
                    if (!module.Details.Any(d => d.PermissionKey == detailKey))
                    {
                        var detail = new PermissionDetail { PermissionModuleId = module.Id, PermissionKey = detailKey, Name = detailKey };
                        db.PermissionDetails.Add(detail);
                        module.Details.Add(detail);
                    }
                await db.SaveChangesAsync(ct);
                foreach (var role in roleNames)
                {
                    var permission = await db.RoleModulePermissions.Include(p => p.Details)
                        .FirstOrDefaultAsync(p => p.RoleName == role && p.PermissionModuleId == module.Id, ct);
                    var granted = role == "Admin" || (defaultRoles.TryGetValue(key, out var allowed) && allowed.Contains(role));
                    if (permission is null)
                    {
                        permission = new RoleModulePermission { RoleName = role, PermissionModuleId = module.Id, MenuVisible = granted, CanView = granted, CanCreate = role == "Admin", CanEdit = role == "Admin", CanDelete = role == "Admin" };
                        db.RoleModulePermissions.Add(permission);
                        await db.SaveChangesAsync(ct);
                    }
                    foreach (var detail in await db.PermissionDetails.Where(d => d.PermissionModuleId == module.Id).ToListAsync(ct))
                        if (!permission.Details.Any(d => d.PermissionDetailId == detail.Id))
                            db.RolePermissionDetails.Add(new RolePermissionDetail { RoleModulePermissionId = permission.Id, PermissionDetailId = detail.Id, IsGranted = role == "Admin" });
                }
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
