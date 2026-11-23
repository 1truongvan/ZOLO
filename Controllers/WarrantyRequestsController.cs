using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.DTOs;
using WebApplication1.Middleware;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/warranty-requests")]
[Authorize]
public class WarrantyRequestsController(IWarrantyRequestService warrantyRequestService) : ControllerBase
{
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : -1;

    private IActionResult FromService<T>(WarrantyRequestServiceResult<T> result) =>
        result.Succeeded ? Ok(result.Value) : StatusCode(result.StatusCode, new { message = result.Error });

    [HttpGet("mine"), Authorize(Roles = "Customer,User"), ModulePermission("warranti", "view")]
    public async Task<IActionResult> Mine(CancellationToken ct) => Ok(await warrantyRequestService.GetMineAsync(UserId, ct));

    [HttpPost, Authorize(Roles = "Customer,User"), ModulePermission("warranti", "create")]
    public async Task<IActionResult> Create(WarrantyRequestCreateDto dto, CancellationToken ct)
    {
        var result = await warrantyRequestService.CreateAsync(dto, UserId, ct);
        if (!result.Succeeded) return StatusCode(result.StatusCode, new { message = result.Error });
        var request = result.Value!;
        return CreatedAtAction(nameof(MineDetails), new { id = request.Id }, new { request.Id, request.RequestCode, request.Status, request.CreatedAt });
    }

    [HttpPost("{id:int}/attachments"), Authorize(Roles = "Customer,User"), ModulePermission("warranti", "create")]
    [RequestSizeLimit(27_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 27_000_000)]
    public async Task<IActionResult> UploadAttachments(int id, [FromForm] List<IFormFile> files, CancellationToken ct)
    {
        var result = await warrantyRequestService.AddAttachmentsAsync(id, UserId, files, ct);
        return result.Succeeded ? Ok(new { uploaded = result.Value }) : StatusCode(result.StatusCode, new { message = result.Error });
    }

    [HttpGet("{id:int}/attachments/{attachmentId:int}"), Authorize(Roles = "Admin,Manager,Technician,Customer,User"), ModulePermission("warranti", "view")]
    public async Task<IActionResult> GetAttachment(int id, int attachmentId, CancellationToken ct)
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role") ?? string.Empty;
        var result = await warrantyRequestService.GetAttachmentAsync(id, attachmentId, UserId, role, ct);
        if (!result.Succeeded) return StatusCode(result.StatusCode, new { message = result.Error });
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Value!.Content, result.Value.ContentType);
    }

    [HttpGet, Authorize(Roles = "Admin,Manager"), ModulePermission("warranti", "view")]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(await warrantyRequestService.GetListAsync(status, search, page, pageSize, ct));

    [HttpGet("summary"), Authorize(Roles = "Admin,Manager"), ModulePermission("warranti", "view")]
    public async Task<IActionResult> Summary(CancellationToken ct) => Ok(await warrantyRequestService.GetSummaryAsync(ct));

    [HttpGet("{id:int}"), Authorize(Roles = "Admin,Manager"), ModulePermission("warranti", "view")]
    public async Task<IActionResult> GetForManager(int id, CancellationToken ct) => FromService(await warrantyRequestService.GetForManagerAsync(id, ct));

    [HttpGet("{id:int}/customer-history"), Authorize(Roles = "Admin,Manager"), ModulePermission("customer", "view")]
    public async Task<IActionResult> CustomerHistory(int id, CancellationToken ct) => FromService(await warrantyRequestService.GetCustomerHistoryAsync(id, ct));

    [HttpPost("{id:int}/review"), Authorize(Roles = "Admin,Manager"), ModulePermission("warranti:approve", "edit")]
    public async Task<IActionResult> Review(int id, WarrantyReviewDto dto, CancellationToken ct) => FromService(await warrantyRequestService.ReviewAsync(id, dto, ct));

    [HttpPost("{id:int}/assign"), Authorize(Roles = "Admin,Manager"), ModulePermission("warranti:manage-technicians", "edit")]
    public async Task<IActionResult> Assign(int id, WarrantyAssignmentDto dto, CancellationToken ct) => FromService(await warrantyRequestService.AssignAsync(id, dto, ct));

    [HttpGet("assigned"), Authorize(Roles = "Technician"), ModulePermission("warranti", "view")]
    public async Task<IActionResult> Assigned(CancellationToken ct) => Ok(await warrantyRequestService.GetAssignedAsync(UserId, ct));

    [HttpGet("fault-groups"), Authorize(Roles = "Technician,Admin,Manager"), ModulePermission("warranti", "view")]
    public async Task<IActionResult> FaultGroups(CancellationToken ct) => Ok(await warrantyRequestService.GetFaultGroupsAsync(ct));

    [HttpGet("technicians"), Authorize(Roles = "Admin,Manager"), ModulePermission("warranti:manage-technicians", "view")]
    public async Task<IActionResult> Technicians(CancellationToken ct) => Ok(await warrantyRequestService.GetTechniciansAsync(ct));

    [HttpGet("mine/{id:int}"), Authorize(Roles = "Customer,User"), ModulePermission("warranti", "view")]
    public async Task<IActionResult> MineDetails(int id, CancellationToken ct) => FromService(await warrantyRequestService.GetMineDetailsAsync(id, UserId, ct));

    [HttpPut("{id:int}/repair"), Authorize(Roles = "Technician"), ModulePermission("warranti", "edit")]
    public async Task<IActionResult> UpdateRepair(int id, WarrantyRepairUpdateDto dto, CancellationToken ct) => FromService(await warrantyRequestService.UpdateRepairAsync(id, UserId, dto, ct));

    [HttpPost("{id:int}/submit-result"), Authorize(Roles = "Technician"), ModulePermission("warranti", "edit")]
    public async Task<IActionResult> SubmitResult(int id, CancellationToken ct) => FromService(await warrantyRequestService.SubmitResultAsync(id, UserId, ct));

    [HttpPost("{id:int}/complete"), Authorize(Roles = "Admin,Manager"), ModulePermission("warranti:close", "edit")]
    public async Task<IActionResult> Complete(int id, WarrantyCompletionDto dto, CancellationToken ct) => FromService(await warrantyRequestService.CompleteAsync(id, dto, ct));

    [HttpPost("{id:int}/reject-after-inspection"), Authorize(Roles = "Admin,Manager"), ModulePermission("warranti:close", "edit")]
    public async Task<IActionResult> RejectAfterInspection(int id, WarrantyRejectionDto dto, CancellationToken ct) => FromService(await warrantyRequestService.RejectAfterInspectionAsync(id, dto, ct));
}
