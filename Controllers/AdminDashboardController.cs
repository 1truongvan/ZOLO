using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Middleware;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/admin/dashboard")]
    [Authorize(Roles = "Admin,Manager")]
    [ModulePermission("dashboard", "view")]
    public class AdminDashboardController : ControllerBase
    {
        private readonly AppDbContext _db;

        public AdminDashboardController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
        {
            var productTypes = await _db.Products
                .Where(product => product.Category != "")
                .Select(product => product.Category)
                .Distinct()
                .CountAsync(cancellationToken);

            var models = await _db.Products
                .Where(product => product.Model != "")
                .Select(product => product.Model)
                .Distinct()
                .CountAsync(cancellationToken);

            var products = await _db.Products.CountAsync(cancellationToken);
            var accounts = await _db.Users.CountAsync(cancellationToken);
            var customers = await _db.Users
                .CountAsync(user => user.Role == "Customer" || user.Role == "User", cancellationToken);
            var warrantyTickets = await _db.WarrantyRequests.CountAsync(cancellationToken);
            var components = await _db.AdminCatalogEntries
                .CountAsync(item => item.ResourceKey == "components", cancellationToken);
            var warrantyStatuses = await _db.WarrantyRequests
                .GroupBy(request => request.Status)
                .Select(group => new { Status = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);

            return Ok(new
            {
                counts = new
                {
                    productTypes,
                    productGroups = (int?)null,
                    models,
                    products,
                    warrantyTickets,
                    accounts,
                    customers,
                    components
                },
                warrantyStatuses,
                unavailableMetrics = new Dictionary<string, string>
                {
                    ["productGroups"] = "Chưa có entity hoặc trường nhóm sản phẩm.",
                }
            });
        }
    }
}
