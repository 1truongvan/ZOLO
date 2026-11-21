using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.DTOs;
using WebApplication1.Models;
using WebApplication1.Middleware;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/warranty-requests")]
[Authorize]
public class WarrantyRequestsController(AppDbContext db) : ControllerBase
{
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:-1;

    [HttpGet("mine"),Authorize(Roles="Customer,User"),ModulePermission("warranti", "view")]
    public async Task<IActionResult> Mine(CancellationToken ct)=>Ok(await SafeQuery(false).Where(r=>r.CustomerUserId==UserId).OrderByDescending(r=>r.CreatedAt).ToListAsync(ct));

    [HttpPost,Authorize(Roles="Customer,User"),ModulePermission("warranti", "create")]
    public async Task<IActionResult> Create(WarrantyRequestCreateDto dto,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var product=await db.Products.FirstOrDefaultAsync(p=>p.Id==dto.ProductId&&p.OwnerUserId==UserId,ct);if(product is null)return BadRequest(new{message="Chỉ gửi yêu cầu cho sản phẩm đã gắn với tài khoản của bạn."});
        if(await db.WarrantyRequests.AnyAsync(request=>request.CustomerUserId==UserId&&request.ProductId==product.Id&&new[]{"Submitted","Approved","InProgress","ReadyForReview"}.Contains(request.Status),ct))return Conflict(new{message="Sản phẩm này đang có một yêu cầu bảo hành chưa hoàn tất."});
        var request=new WarrantyRequest{RequestCode=$"WR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..28],CustomerUserId=UserId,ProductId=product.Id,Description=dto.Description.Trim()};db.WarrantyRequests.Add(request);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return CreatedAtAction(nameof(MineDetails),new{id=request.Id},new{request.Id,request.RequestCode,request.Status,request.CreatedAt});
    }

    [HttpGet,Authorize(Roles="Admin,Manager"),ModulePermission("warranti", "view")]
    public async Task<IActionResult> List([FromQuery]string? status,[FromQuery]string? search,[FromQuery]int page=1,[FromQuery]int pageSize=25,CancellationToken ct=default)
    {
        page=Math.Max(1,page);pageSize=Math.Clamp(pageSize,1,100);var query=Query();if(!string.IsNullOrWhiteSpace(status))query=query.Where(r=>r.Status==status);if(!string.IsNullOrWhiteSpace(search))query=query.Where(r=>r.RequestCode.Contains(search)||r.Customer.Username.Contains(search)||r.Customer.Email.Contains(search));var total=await query.CountAsync(ct);var items=await query.OrderByDescending(r=>r.CreatedAt).Skip((page-1)*pageSize).Take(pageSize).Select(r=>new{r.Id,r.RequestCode,r.Description,r.Status,r.CreatedAt,r.UpdatedAt,Customer=r.Customer.FullName??r.Customer.Username,CustomerEmail=r.Customer.Email,Product=r.Product==null?null:r.Product.ProductName,SerialNumber=r.Product==null?null:r.Product.SerialNumber,Technician=r.AssignedTechnician==null?null:r.AssignedTechnician.FullName??r.AssignedTechnician.Username}).ToListAsync(ct);return Ok(new{items,total,page,pageSize,totalPages=(int)Math.Ceiling(total/(double)pageSize)});
    }

    [HttpGet("summary"),Authorize(Roles="Admin,Manager"),ModulePermission("warranti", "view")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var requests=await db.WarrantyRequests.AsNoTracking().Select(request=>request.Status).ToListAsync(ct);
        var technicianCount=await db.Users.CountAsync(user=>user.Role=="Technician"&&user.IsActive,ct);
        var latest=await Query().OrderByDescending(request=>request.CreatedAt).Take(8).Select(request=>new{request.Id,request.RequestCode,request.Status,request.CreatedAt,Customer=request.Customer.FullName??request.Customer.Username,Product=request.Product==null?null:request.Product.ProductName}).ToListAsync(ct);
        return Ok(new{total=requests.Count,pending=requests.Count(status=>status=="Submitted"),inProgress=requests.Count(status=>status=="InProgress"),readyForReview=requests.Count(status=>status=="ReadyForReview"),completed=requests.Count(status=>status=="Completed"),technicians=technicianCount,latest});
    }

    [HttpGet("{id:int}"),Authorize(Roles="Admin,Manager"),ModulePermission("warranti", "view")]
    public async Task<IActionResult> GetForManager(int id,CancellationToken ct){var item=await SafeQuery().FirstOrDefaultAsync(r=>r.Id==id,ct);return item is null?NotFound():Ok(item);}

    [HttpGet("{id:int}/customer-history"),Authorize(Roles="Admin,Manager"),ModulePermission("customer", "view")]
    public async Task<IActionResult> CustomerHistory(int id,CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(user => user.Id == id, ct)) return NotFound();
        var history = await db.WarrantyRequests.AsNoTracking()
            .Where(request => request.CustomerUserId == id)
            .OrderByDescending(request => request.CreatedAt)
            .Select(request => new
            {
                request.Id,
                request.RequestCode,
                request.Description,
                request.Status,
                request.CreatedAt,
                request.UpdatedAt,
                ProductName = request.Product == null ? null : request.Product.ProductName,
                SerialNumber = request.Product == null ? null : request.Product.SerialNumber
            })
            .ToListAsync(ct);
        return Ok(history);
    }

    [HttpPost("{id:int}/review"),Authorize(Roles="Admin,Manager"),ModulePermission("warranti:approve", "edit")]
    public async Task<IActionResult> Review(int id,WarrantyReviewDto dto,CancellationToken ct){var item=await db.WarrantyRequests.FirstOrDefaultAsync(r=>r.Id==id,ct);if(item is null)return NotFound();if(item.Status!="Submitted")return Conflict(new{message="Chỉ yêu cầu mới gửi mới được duyệt hoặc từ chối."});item.Status=dto.Approved?"Approved":"Rejected";item.ManagerNotes=dto.Note?.Trim();item.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);return Ok(new{item.Id,item.Status,item.ManagerNotes});}

    [HttpPost("{id:int}/assign"),Authorize(Roles="Admin,Manager"),ModulePermission("warranti:manage-technicians", "edit")]
    public async Task<IActionResult> Assign(int id,WarrantyAssignmentDto dto,CancellationToken ct){var item=await db.WarrantyRequests.FirstOrDefaultAsync(r=>r.Id==id,ct);if(item is null)return NotFound();if(item.Status!="Approved")return Conflict(new{message="Yêu cầu cần được duyệt trước khi phân công."});var tech=await db.Users.FirstOrDefaultAsync(u=>u.Id==dto.TechnicianId&&u.Role=="Technician"&&u.IsActive,ct);if(tech is null)return BadRequest(new{message="Không tìm thấy kỹ thuật viên đang hoạt động."});item.AssignedTechnicianId=tech.Id;item.Status="InProgress";item.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);return Ok(new{item.Id,item.Status,Technician=tech.Username});}

    [HttpGet("assigned"),Authorize(Roles="Technician"),ModulePermission("warranti", "view")]
    public async Task<IActionResult> Assigned(CancellationToken ct)=>Ok(await SafeQuery().Where(r=>r.AssignedTechnicianId==UserId).OrderByDescending(r=>r.UpdatedAt).ToListAsync(ct));

    [HttpGet("technicians"),Authorize(Roles="Admin,Manager"),ModulePermission("warranti:manage-technicians", "view")]
    public async Task<IActionResult> Technicians(CancellationToken ct)=>Ok(await db.Users.AsNoTracking().Where(user=>user.Role=="Technician"&&user.IsActive).OrderBy(user=>user.FullName??user.Username).Select(user=>new{user.Id,user.Username,user.FullName}).ToListAsync(ct));

    [HttpGet("mine/{id:int}"),Authorize(Roles="Customer,User"),ModulePermission("warranti", "view")]
    public async Task<IActionResult> MineDetails(int id,CancellationToken ct){var item=await SafeQuery(false).FirstOrDefaultAsync(request=>request.Id==id&&request.CustomerUserId==UserId,ct);return item is null?NotFound():Ok(item);}

    [HttpPut("{id:int}/repair"),Authorize(Roles="Technician"),ModulePermission("warranti", "edit")]
    public async Task<IActionResult> UpdateRepair(int id,WarrantyRepairUpdateDto dto,CancellationToken ct){var item=await db.WarrantyRequests.FirstOrDefaultAsync(r=>r.Id==id&&r.AssignedTechnicianId==UserId,ct);if(item is null)return NotFound();if(item.Status!="InProgress")return Conflict(new{message="Phiếu hiện không ở trạng thái sửa chữa."});item.InspectionResult=dto.InspectionResult.Trim();item.FaultCause=dto.FaultCause?.Trim();item.RepairNotes=dto.RepairNotes?.Trim();item.Resolution=dto.Resolution?.Trim();item.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);return Ok(new{item.Id,item.Status,item.UpdatedAt});}

    [HttpPost("{id:int}/submit-result"),Authorize(Roles="Technician"),ModulePermission("warranti", "edit")]
    public async Task<IActionResult> SubmitResult(int id,CancellationToken ct){var item=await db.WarrantyRequests.FirstOrDefaultAsync(r=>r.Id==id&&r.AssignedTechnicianId==UserId,ct);if(item is null)return NotFound();if(item.Status!="InProgress"||string.IsNullOrWhiteSpace(item.InspectionResult)||string.IsNullOrWhiteSpace(item.Resolution))return Conflict(new{message="Cần nhập kết quả kiểm tra và kết quả xử lý trước khi gửi."});item.Status="ReadyForReview";item.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);return Ok(new{item.Id,item.Status});}

    [HttpPost("{id:int}/complete"),Authorize(Roles="Admin,Manager"),ModulePermission("warranti:close", "edit")]
    public async Task<IActionResult> Complete(int id,WarrantyCompletionDto dto,CancellationToken ct){var item=await db.WarrantyRequests.FirstOrDefaultAsync(r=>r.Id==id,ct);if(item is null)return NotFound();if(item.Status!="ReadyForReview")return Conflict(new{message="Chỉ kết quả đang chờ Manager kiểm tra mới được xác nhận."});item.ManagerNotes=dto.Note?.Trim();item.Status=dto.Completed?"Completed":"InProgress";item.CompletedAt=dto.Completed?DateTime.UtcNow:null;item.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);return Ok(new{item.Id,item.Status,item.CompletedAt});}

    private IQueryable<WarrantyRequest> Query()=>db.WarrantyRequests.AsNoTracking().Include(r=>r.Customer).Include(r=>r.Product).Include(r=>r.AssignedTechnician);
    private IQueryable<WarrantyRequestResponse> SafeQuery(bool includeContact=true)=>db.WarrantyRequests.AsNoTracking().Select(request=>new WarrantyRequestResponse
    {
        Id=request.Id,RequestCode=request.RequestCode,CustomerUserId=request.CustomerUserId,
        Customer=new WarrantyRequestPersonResponse{Id=request.Customer.Id,Username=request.Customer.Username,FullName=request.Customer.FullName,Email=includeContact?request.Customer.Email:null},
        ProductId=request.ProductId,Product=request.Product==null?null:new WarrantyRequestProductResponse{Id=request.Product.Id,ProductName=request.Product.ProductName,ProductCode=request.Product.ProductCode,SerialNumber=request.Product.SerialNumber},
        AssignedTechnicianId=request.AssignedTechnicianId,
        AssignedTechnician=request.AssignedTechnician==null?null:new WarrantyRequestPersonResponse{Id=request.AssignedTechnician.Id,Username=request.AssignedTechnician.Username,FullName=request.AssignedTechnician.FullName,Email=includeContact?request.AssignedTechnician.Email:null},
        Description=request.Description,Status=request.Status,InspectionResult=request.InspectionResult,
        FaultCause=request.FaultCause,RepairNotes=request.RepairNotes,Resolution=request.Resolution,
        ManagerNotes=includeContact?request.ManagerNotes:null,CreatedAt=request.CreatedAt,UpdatedAt=request.UpdatedAt,CompletedAt=request.CompletedAt
    });
}
