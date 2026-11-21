using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Seeding;

public static class AdminCatalogSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        var rows = new (string Resource, string Code, string Name)[]
        {
            ("brands","ASUS","ASUS"),("brands","APPLE","Apple"),("brands","SONY","Sony"),
            ("categories","LAPTOP","Laptop"),("categories","PHONE","Điện thoại"),("categories","TABLET","Máy tính bảng"),
            ("categories","AUDIO","Tai nghe"),("categories","WATCH","Đồng hồ"),("categories","ACCESSORY","Phụ kiện điện tử"),
            ("warranty-types","STANDARD","Bảo hành chính hãng"),("warranty-types","EXTENDED","Bảo hành mở rộng"),
            ("warranty-types","REPAIR","Bảo hành sửa chữa"),("warranty-types","PARTS","Bảo hành linh kiện"),
            ("fault-groups","HARDWARE","Lỗi phần cứng"),("fault-groups","SOFTWARE","Lỗi phần mềm"),
            ("fault-groups","DISPLAY","Lỗi màn hình"),("fault-groups","POWER","Lỗi nguồn"),
            ("fault-groups","CONNECTIVITY","Lỗi kết nối"),("fault-groups","BATTERY","Lỗi pin"),("fault-groups","ACCESSORY","Lỗi phụ kiện")
        };
        foreach (var row in rows)
        {
            if (await db.AdminCatalogEntries.AnyAsync(item => item.ResourceKey == row.Resource && item.Code == row.Code, ct)) continue;
            db.AdminCatalogEntries.Add(new AdminCatalogEntry
            {
                ResourceKey = row.Resource, Code = row.Code, Name = row.Name,
                Description = "Danh mục khởi tạo cho môi trường Development.", IsActive = true
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
