using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.DTOs;
using WebApplication1.Models;

namespace WebApplication1.Services;

public sealed record WarrantyRequestServiceResult<T>(T? Value, string? Error, int StatusCode)
{
    public bool Succeeded => Error is null;
    public static WarrantyRequestServiceResult<T> Success(T value) => new(value, null, StatusCodes.Status200OK);
    public static WarrantyRequestServiceResult<T> Failure(string error, int statusCode) => new(default, error, statusCode);
}

public sealed record WarrantyAttachmentContent(byte[] Content, string ContentType, string FileName);
public sealed record WarrantyTechnicianOption(int Id, string Username, string? FullName);
public sealed record WarrantyFaultGroupOption(int Id, string Code, string Name, string? Description);

public interface IWarrantyRequestService
{
    Task<List<WarrantyRequestResponse>> GetMineAsync(int userId, CancellationToken ct);
    Task<WarrantyRequestServiceResult<WarrantyRequest>> CreateAsync(WarrantyRequestCreateDto dto, int userId, CancellationToken ct);
    Task<WarrantyRequestServiceResult<int>> AddAttachmentsAsync(int requestId, int userId, IReadOnlyList<IFormFile> files, CancellationToken ct);
    Task<WarrantyRequestServiceResult<WarrantyAttachmentContent>> GetAttachmentAsync(int requestId, int attachmentId, int userId, string role, CancellationToken ct);
    Task<object> GetListAsync(string? status, string? search, int page, int pageSize, CancellationToken ct);
    Task<object> GetSummaryAsync(CancellationToken ct);
    Task<WarrantyRequestServiceResult<WarrantyRequestResponse>> GetForManagerAsync(int id, CancellationToken ct);
    Task<WarrantyRequestServiceResult<object>> GetCustomerHistoryAsync(int userId, CancellationToken ct);
    Task<WarrantyRequestServiceResult<object>> ReviewAsync(int id, WarrantyReviewDto dto, CancellationToken ct);
    Task<WarrantyRequestServiceResult<object>> AssignAsync(int id, WarrantyAssignmentDto dto, CancellationToken ct);
    Task<WarrantyRequestServiceResult<object>> ReassignAsync(int id, WarrantyAssignmentDto dto, CancellationToken ct);
    Task<List<WarrantyRequestResponse>> GetAssignedAsync(int technicianId, CancellationToken ct);
    Task<List<WarrantyFaultGroupOption>> GetFaultGroupsAsync(CancellationToken ct);
    Task<List<WarrantyTechnicianOption>> GetTechniciansAsync(CancellationToken ct);
    Task<WarrantyRequestServiceResult<WarrantyRequestResponse>> GetMineDetailsAsync(int id, int userId, CancellationToken ct);
    Task<WarrantyRequestServiceResult<object>> UpdateRepairAsync(int id, int technicianId, WarrantyRepairUpdateDto dto, CancellationToken ct);
    Task<WarrantyRequestServiceResult<object>> SubmitResultAsync(int id, int technicianId, CancellationToken ct);
    Task<WarrantyRequestServiceResult<object>> CompleteAsync(int id, WarrantyCompletionDto dto, CancellationToken ct);
    Task<WarrantyRequestServiceResult<object>> RejectAfterInspectionAsync(int id, WarrantyRejectionDto dto, CancellationToken ct);
}

public sealed class WarrantyRequestService(AppDbContext db) : IWarrantyRequestService
{
    private static readonly HashSet<string> AllowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png", "image/webp" };

    public Task<List<WarrantyRequestResponse>> GetMineAsync(int userId, CancellationToken ct) =>
        SafeQuery(false).Where(request => request.CustomerUserId == userId).OrderByDescending(request => request.CreatedAt).ToListAsync(ct);

    public async Task<object> GetListAsync(string? status, string? search, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = Query();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(request => request.Status == status);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(request => request.RequestCode.Contains(search) || request.Customer.Username.Contains(search) || request.Customer.Email.Contains(search));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(request => request.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(request => new
            {
                request.Id, request.RequestCode, request.Description, request.Status, request.CreatedAt, request.UpdatedAt,
                Customer = request.Customer.FullName ?? request.Customer.Username, CustomerEmail = request.Customer.Email,
                Product = request.Product == null ? null : request.Product.ProductName,
                SerialNumber = request.Product == null ? null : request.Product.SerialNumber,
                Technician = request.AssignedTechnician == null ? null : request.AssignedTechnician.FullName ?? request.AssignedTechnician.Username,
                AttachmentCount = request.Attachments.Count,
                HasTechnicianDiagnosis = request.InspectionResult != null || request.FaultCause != null
            }).ToListAsync(ct);
        return new { items, total, page, pageSize, totalPages = (int)Math.Ceiling(total / (double)pageSize) };
    }

    public async Task<object> GetSummaryAsync(CancellationToken ct)
    {
        var statuses = await db.WarrantyRequests.AsNoTracking().Select(request => request.Status).ToListAsync(ct);
        var technicianCount = await db.Users.CountAsync(user => user.Role == "Technician" && user.IsActive, ct);
        var latest = await Query().OrderByDescending(request => request.CreatedAt).Take(8)
            .Select(request => new { request.Id, request.RequestCode, request.Status, request.CreatedAt, Customer = request.Customer.FullName ?? request.Customer.Username, Product = request.Product == null ? null : request.Product.ProductName })
            .ToListAsync(ct);
        return new { total = statuses.Count, pending = statuses.Count(status => status == "Submitted"), inProgress = statuses.Count(status => status == "InProgress"), readyForReview = statuses.Count(status => status == "ReadyForReview"), completed = statuses.Count(status => status == "Completed"), technicians = technicianCount, latest };
    }

    public async Task<WarrantyRequestServiceResult<WarrantyRequestResponse>> GetForManagerAsync(int id, CancellationToken ct)
    {
        var item = await SafeQuery().FirstOrDefaultAsync(request => request.Id == id, ct);
        return item is null ? WarrantyRequestServiceResult<WarrantyRequestResponse>.Failure("Không tìm thấy yêu cầu.", StatusCodes.Status404NotFound) : WarrantyRequestServiceResult<WarrantyRequestResponse>.Success(item);
    }

    public async Task<WarrantyRequestServiceResult<object>> GetCustomerHistoryAsync(int userId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(user => user.Id == userId, ct)) return WarrantyRequestServiceResult<object>.Failure("Không tìm thấy khách hàng.", StatusCodes.Status404NotFound);
        var history = await db.WarrantyRequests.AsNoTracking().Where(request => request.CustomerUserId == userId).OrderByDescending(request => request.CreatedAt)
            .Select(request => new { request.Id, request.RequestCode, request.Description, request.Status, request.CreatedAt, request.UpdatedAt, ProductName = request.Product == null ? null : request.Product.ProductName, SerialNumber = request.Product == null ? null : request.Product.SerialNumber })
            .ToListAsync(ct);
        return WarrantyRequestServiceResult<object>.Success(history);
    }

    public async Task<WarrantyRequestServiceResult<object>> ReviewAsync(int id, WarrantyReviewDto dto, CancellationToken ct)
    {
        if (!dto.Approved && string.IsNullOrWhiteSpace(dto.Note))
            return WarrantyRequestServiceResult<object>.Failure("Cần nhập lý do từ chối để khách hàng biết quyết định.", StatusCodes.Status400BadRequest);
        var item = await db.WarrantyRequests.FirstOrDefaultAsync(request => request.Id == id, ct);
        if (item is null) return WarrantyRequestServiceResult<object>.Failure("Không tìm thấy yêu cầu.", StatusCodes.Status404NotFound);
        if (item.Status != "Submitted") return WarrantyRequestServiceResult<object>.Failure("Chỉ yêu cầu mới gửi mới được duyệt hoặc từ chối.", StatusCodes.Status409Conflict);
        item.Status = dto.Approved ? "Approved" : "Rejected"; item.ManagerNotes = dto.Note?.Trim(); item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return WarrantyRequestServiceResult<object>.Success(new { item.Id, item.Status, item.ManagerNotes });
    }

    public async Task<WarrantyRequestServiceResult<object>> AssignAsync(int id, WarrantyAssignmentDto dto, CancellationToken ct)
    {
        var item = await db.WarrantyRequests.FirstOrDefaultAsync(request => request.Id == id, ct);
        if (item is null) return WarrantyRequestServiceResult<object>.Failure("Không tìm thấy yêu cầu.", StatusCodes.Status404NotFound);
        if (item.Status != "Approved") return WarrantyRequestServiceResult<object>.Failure("Yêu cầu cần được duyệt trước khi phân công.", StatusCodes.Status409Conflict);
        var technician = await db.Users.FirstOrDefaultAsync(user => user.Id == dto.TechnicianId && user.Role == "Technician" && user.IsActive, ct);
        if (technician is null) return WarrantyRequestServiceResult<object>.Failure("Không tìm thấy kỹ thuật viên đang hoạt động.", StatusCodes.Status400BadRequest);
        item.AssignedTechnicianId = technician.Id; item.Status = "InProgress"; item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return WarrantyRequestServiceResult<object>.Success(new { item.Id, item.Status, Technician = technician.Username });
    }

    public async Task<WarrantyRequestServiceResult<object>> ReassignAsync(int id, WarrantyAssignmentDto dto, CancellationToken ct)
    {
        var item = await db.WarrantyRequests.FirstOrDefaultAsync(request => request.Id == id, ct);
        if (item is null)
            return WarrantyRequestServiceResult<object>.Failure("Không tìm thấy yêu cầu.", StatusCodes.Status404NotFound);
        if (item.Status != "InProgress" || !item.AssignedTechnicianId.HasValue)
            return WarrantyRequestServiceResult<object>.Failure("Chỉ có thể giao lại phiếu đang được kỹ thuật viên xử lý.", StatusCodes.Status409Conflict);
        if (item.AssignedTechnicianId.Value == dto.TechnicianId)
            return WarrantyRequestServiceResult<object>.Failure("Hãy chọn kỹ thuật viên khác với người đang phụ trách.", StatusCodes.Status400BadRequest);

        var technician = await db.Users.FirstOrDefaultAsync(
            user => user.Id == dto.TechnicianId && user.Role == "Technician" && user.IsActive, ct);
        if (technician is null)
            return WarrantyRequestServiceResult<object>.Failure("Không tìm thấy kỹ thuật viên đang hoạt động.", StatusCodes.Status400BadRequest);

        var previousTechnicianId = item.AssignedTechnicianId.Value;
        item.AssignedTechnicianId = technician.Id;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return WarrantyRequestServiceResult<object>.Success(new
        {
            item.Id,
            item.RequestCode,
            item.Status,
            PreviousTechnicianId = previousTechnicianId,
            TechnicianId = technician.Id,
            Technician = technician.FullName ?? technician.Username
        });
    }

    public Task<List<WarrantyRequestResponse>> GetAssignedAsync(int technicianId, CancellationToken ct) =>
        SafeQuery().Where(request => request.AssignedTechnicianId == technicianId).OrderByDescending(request => request.UpdatedAt).ToListAsync(ct);

    public Task<List<WarrantyFaultGroupOption>> GetFaultGroupsAsync(CancellationToken ct) =>
        db.AdminCatalogEntries.AsNoTracking()
            .Where(entry => entry.ResourceKey == "fault-groups" && entry.IsActive)
            .OrderBy(entry => entry.Name)
            .Select(entry => new WarrantyFaultGroupOption(entry.Id, entry.Code, entry.Name, entry.Description))
            .ToListAsync(ct);

    public async Task<List<WarrantyTechnicianOption>> GetTechniciansAsync(CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(user => user.Role == "Technician" && user.IsActive).OrderBy(user => user.FullName ?? user.Username)
            .Select(user => new WarrantyTechnicianOption(user.Id, user.Username, user.FullName)).ToListAsync(ct);

    public async Task<WarrantyRequestServiceResult<WarrantyRequestResponse>> GetMineDetailsAsync(int id, int userId, CancellationToken ct)
    {
        var item = await SafeQuery(false).FirstOrDefaultAsync(request => request.Id == id && request.CustomerUserId == userId, ct);
        return item is null ? WarrantyRequestServiceResult<WarrantyRequestResponse>.Failure("Không tìm thấy yêu cầu.", StatusCodes.Status404NotFound) : WarrantyRequestServiceResult<WarrantyRequestResponse>.Success(item);
    }

    public async Task<WarrantyRequestServiceResult<object>> UpdateRepairAsync(int id, int technicianId, WarrantyRepairUpdateDto dto, CancellationToken ct)
    {
        var item = await db.WarrantyRequests.FirstOrDefaultAsync(request => request.Id == id && request.AssignedTechnicianId == technicianId, ct);
        if (item is null) return WarrantyRequestServiceResult<object>.Failure("Không tìm thấy công việc được giao.", StatusCodes.Status404NotFound);
        if (item.Status != "InProgress") return WarrantyRequestServiceResult<object>.Failure("Phiếu hiện không ở trạng thái sửa chữa.", StatusCodes.Status409Conflict);
        if (!dto.FaultGroupId.HasValue || !await db.AdminCatalogEntries.AnyAsync(entry => entry.Id == dto.FaultGroupId.Value && entry.ResourceKey == "fault-groups" && entry.IsActive, ct))
            return WarrantyRequestServiceResult<object>.Failure("Vui lòng chọn nhóm lỗi đang hoạt động.", StatusCodes.Status400BadRequest);
        item.FaultGroupId = dto.FaultGroupId;
        item.InspectionResult = dto.InspectionResult.Trim(); item.FaultCause = dto.FaultCause?.Trim(); item.RepairNotes = dto.RepairNotes?.Trim(); item.Resolution = dto.Resolution?.Trim(); item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return WarrantyRequestServiceResult<object>.Success(new { item.Id, item.Status, item.UpdatedAt });
    }

    public async Task<WarrantyRequestServiceResult<object>> SubmitResultAsync(int id, int technicianId, CancellationToken ct)
    {
        var item = await db.WarrantyRequests.FirstOrDefaultAsync(request => request.Id == id && request.AssignedTechnicianId == technicianId, ct);
        if (item is null) return WarrantyRequestServiceResult<object>.Failure("Không tìm thấy công việc được giao.", StatusCodes.Status404NotFound);
        if (item.Status != "InProgress" || string.IsNullOrWhiteSpace(item.InspectionResult) || string.IsNullOrWhiteSpace(item.Resolution))
            return WarrantyRequestServiceResult<object>.Failure("Cần nhập kết quả kiểm tra và kết quả xử lý trước khi gửi.", StatusCodes.Status409Conflict);
        item.Status = "ReadyForReview"; item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return WarrantyRequestServiceResult<object>.Success(new { item.Id, item.Status });
    }

    public async Task<WarrantyRequestServiceResult<object>> CompleteAsync(int id, WarrantyCompletionDto dto, CancellationToken ct)
    {
        var item = await db.WarrantyRequests.FirstOrDefaultAsync(request => request.Id == id, ct);
        if (item is null) return WarrantyRequestServiceResult<object>.Failure("Không tìm thấy yêu cầu.", StatusCodes.Status404NotFound);
        if (item.Status != "ReadyForReview") return WarrantyRequestServiceResult<object>.Failure("Chỉ kết quả đang chờ Manager kiểm tra mới được xác nhận.", StatusCodes.Status409Conflict);
        item.ManagerNotes = dto.Note?.Trim(); item.Status = dto.Completed ? "Completed" : "InProgress"; item.CompletedAt = dto.Completed ? DateTime.UtcNow : null; item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return WarrantyRequestServiceResult<object>.Success(new { item.Id, item.Status, item.CompletedAt });
    }

    public async Task<WarrantyRequestServiceResult<object>> RejectAfterInspectionAsync(int id, WarrantyRejectionDto dto, CancellationToken ct)
    {
        var item = await db.WarrantyRequests.FirstOrDefaultAsync(request => request.Id == id, ct);
        if (item is null) return WarrantyRequestServiceResult<object>.Failure("Không tìm thấy yêu cầu.", StatusCodes.Status404NotFound);
        if (item.Status is not ("InProgress" or "ReadyForReview") || (string.IsNullOrWhiteSpace(item.InspectionResult) && string.IsNullOrWhiteSpace(item.FaultCause)))
            return WarrantyRequestServiceResult<object>.Failure("Chỉ có thể từ chối sau khi kỹ thuật viên đã lưu chẩn đoán.", StatusCodes.Status409Conflict);
        item.Status = "Rejected";
        item.ManagerNotes = dto.Reason.Trim();
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return WarrantyRequestServiceResult<object>.Success(new { item.Id, item.Status, item.ManagerNotes, item.UpdatedAt });
    }

    public async Task<WarrantyRequestServiceResult<WarrantyRequest>> CreateAsync(WarrantyRequestCreateDto dto, int userId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.OwnerUserId == userId, ct);
        if (product is null)
            return WarrantyRequestServiceResult<WarrantyRequest>.Failure("Chỉ gửi yêu cầu cho sản phẩm đã gắn với tài khoản của bạn.", StatusCodes.Status400BadRequest);

        var activeStatuses = new[] { "Submitted", "Approved", "InProgress", "ReadyForReview" };
        if (await db.WarrantyRequests.AnyAsync(r => r.CustomerUserId == userId && r.ProductId == product.Id && activeStatuses.Contains(r.Status), ct))
            return WarrantyRequestServiceResult<WarrantyRequest>.Failure("Sản phẩm này đang có một yêu cầu bảo hành chưa hoàn tất.", StatusCodes.Status409Conflict);

        var request = new WarrantyRequest
        {
            RequestCode = $"WR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..28],
            CustomerUserId = userId,
            ProductId = product.Id,
            Description = dto.Description.Trim()
        };
        db.WarrantyRequests.Add(request);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return WarrantyRequestServiceResult<WarrantyRequest>.Success(request);
    }

    public async Task<WarrantyRequestServiceResult<int>> AddAttachmentsAsync(int requestId, int userId, IReadOnlyList<IFormFile> files, CancellationToken ct)
    {
        if (files.Count is < 1 or > 5)
            return WarrantyRequestServiceResult<int>.Failure("Chọn từ 1 đến 5 ảnh.", StatusCodes.Status400BadRequest);
        if (files.Any(file => file.Length == 0 || file.Length > 5 * 1024 * 1024))
            return WarrantyRequestServiceResult<int>.Failure("Mỗi ảnh phải có dung lượng từ 1 byte đến 5 MB.", StatusCodes.Status400BadRequest);
        if (files.Any(file => !AllowedImageTypes.Contains(file.ContentType)))
            return WarrantyRequestServiceResult<int>.Failure("Chỉ chấp nhận ảnh JPG, PNG hoặc WEBP.", StatusCodes.Status400BadRequest);

        if (!await db.WarrantyRequests.AnyAsync(item => item.Id == requestId && item.CustomerUserId == userId, ct))
            return WarrantyRequestServiceResult<int>.Failure("Không tìm thấy yêu cầu bảo hành.", StatusCodes.Status404NotFound);
        var currentCount = await db.WarrantyRequestAttachments.CountAsync(item => item.WarrantyRequestId == requestId, ct);
        if (currentCount + files.Count > 5)
            return WarrantyRequestServiceResult<int>.Failure("Mỗi yêu cầu được đính kèm tối đa 5 ảnh.", StatusCodes.Status400BadRequest);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var file in files)
        {
            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream, ct);
            var storedFile = new StoredFile
            {
                FileName = Path.GetFileName(file.FileName),
                ContentType = file.ContentType.ToLowerInvariant(),
                Content = stream.ToArray(),
                Length = file.Length,
                UploadedAt = DateTime.UtcNow
            };
            db.StoredFiles.Add(storedFile);
            db.WarrantyRequestAttachments.Add(new WarrantyRequestAttachment { WarrantyRequestId = requestId, StoredFile = storedFile });
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return WarrantyRequestServiceResult<int>.Success(files.Count);
    }

    public async Task<WarrantyRequestServiceResult<WarrantyAttachmentContent>> GetAttachmentAsync(int requestId, int attachmentId, int userId, string role, CancellationToken ct)
    {
        var item = await db.WarrantyRequestAttachments.AsNoTracking()
            .Where(attachment => attachment.Id == attachmentId && attachment.WarrantyRequestId == requestId)
            .Select(attachment => new
            {
                attachment.WarrantyRequest.CustomerUserId,
                attachment.WarrantyRequest.AssignedTechnicianId,
                attachment.StoredFile.ContentType,
                attachment.StoredFile.FileName,
                attachment.StoredFile.Content
            })
            .FirstOrDefaultAsync(ct);
        if (item is null)
            return WarrantyRequestServiceResult<WarrantyAttachmentContent>.Failure("Không tìm thấy ảnh.", StatusCodes.Status404NotFound);

        var allowed = role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Manager", StringComparison.OrdinalIgnoreCase)
            || (role.Equals("Technician", StringComparison.OrdinalIgnoreCase) && item.AssignedTechnicianId == userId)
            || ((role.Equals("Customer", StringComparison.OrdinalIgnoreCase) || role.Equals("User", StringComparison.OrdinalIgnoreCase)) && item.CustomerUserId == userId);
        if (!allowed)
            return WarrantyRequestServiceResult<WarrantyAttachmentContent>.Failure("Bạn không có quyền xem ảnh này.", StatusCodes.Status403Forbidden);
        if (!AllowedImageTypes.Contains(item.ContentType))
            return WarrantyRequestServiceResult<WarrantyAttachmentContent>.Failure("Định dạng ảnh không hợp lệ.", StatusCodes.Status400BadRequest);

        return WarrantyRequestServiceResult<WarrantyAttachmentContent>.Success(new(item.Content, item.ContentType, item.FileName));
    }

    private IQueryable<WarrantyRequest> Query() => db.WarrantyRequests.AsNoTracking()
        .Include(request => request.Customer).Include(request => request.Product).Include(request => request.AssignedTechnician);

    private IQueryable<WarrantyRequestResponse> SafeQuery(bool includeContact = true) => db.WarrantyRequests.AsNoTracking().Select(request => new WarrantyRequestResponse
    {
        Id = request.Id, RequestCode = request.RequestCode, CustomerUserId = request.CustomerUserId,
        Customer = new WarrantyRequestPersonResponse { Id = request.Customer.Id, Username = request.Customer.Username, FullName = request.Customer.FullName, Email = includeContact ? request.Customer.Email : null },
        ProductId = request.ProductId,
        Product = request.Product == null ? null : new WarrantyRequestProductResponse { Id = request.Product.Id, ProductName = request.Product.ProductName, ProductCode = request.Product.ProductCode, SerialNumber = request.Product.SerialNumber },
        AssignedTechnicianId = request.AssignedTechnicianId,
        AssignedTechnician = request.AssignedTechnician == null ? null : new WarrantyRequestPersonResponse { Id = request.AssignedTechnician.Id, Username = request.AssignedTechnician.Username, FullName = request.AssignedTechnician.FullName, Email = includeContact ? request.AssignedTechnician.Email : null },
        Description = request.Description, Status = request.Status, InspectionResult = request.InspectionResult,
        FaultGroupId = request.FaultGroupId,
        FaultGroupName = request.FaultGroup == null ? null : request.FaultGroup.Name,
        FaultCause = request.FaultCause,
        RepairNotes = request.RepairNotes, Resolution = request.Resolution, ManagerNotes = includeContact ? request.ManagerNotes : null,
        RejectionReason = request.Status == "Rejected" ? request.ManagerNotes : null,
        CreatedAt = request.CreatedAt, UpdatedAt = request.UpdatedAt, CompletedAt = request.CompletedAt,
        Attachments = request.Attachments.OrderBy(attachment => attachment.Id).Select(attachment => new WarrantyRequestAttachmentResponse
        {
            Id = attachment.Id, FileName = attachment.StoredFile.FileName, ContentType = attachment.StoredFile.ContentType,
            Length = attachment.StoredFile.Length, UploadedAt = attachment.UploadedAt
        }).ToList()
    });
}
