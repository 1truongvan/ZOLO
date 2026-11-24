using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.DTOs;
using WebApplication1.Models;

namespace WebApplication1.Services;

public interface IWarrantyChatService
{

    Task<bool> CanAccessAsync (int requestId, int userId, string role);
    Task<bool> IsChatOpenAsync(int requestId, CancellationToken ct);

    Task<List<WarrantyChatMessageDto>> GetHistoryAsync(
        int requestId, int userId, string role, CancellationToken ct);
    Task<WarrantyChatMessageDto> SendAsync(
        int requestId, int userId, string role, string content, CancellationToken ct);
}

public class WarrantyChatService(AppDbContext db) : IWarrantyChatService
{
    public async Task<bool> CanAccessAsync(int requestId, int userId, string role)
    {
        if (role is "Admin" or "Manager")
            return await db.WarrantyRequests.AnyAsync(r => r.Id == requestId);

        return await db.WarrantyRequests.AnyAsync(r =>
            r.Id == requestId &&
            (r.CustomerUserId == userId || r.AssignedTechnicianId == userId));
    }
    public async Task<bool> IsChatOpenAsync(int requestId, CancellationToken ct)
    {
        var status = await db.WarrantyRequests
            .Where(r => r.Id == requestId)
            .Select(r => r.Status)
            .SingleOrDefaultAsync(ct);

        return status is "Submitted"
            or "Approved"
            or "InProgress"
            or "ReadyForReview";
    }

    public async Task<List<WarrantyChatMessageDto>> GetHistoryAsync(
        int requestId, int userId, string role, CancellationToken ct)
    {
        if (!await CanAccessAsync(requestId, userId, role))
            throw new UnauthorizedAccessException("Bạn không có quyền xem chat của phiếu này.");

        return await db.WarrantyMessages
            .AsNoTracking()
            .Where(m => m.WarrantyRequestId == requestId)
            .OrderBy(m => m.SentAt)
            .Select(m => new WarrantyChatMessageDto
            {
                Id = m.Id,
                WarrantyRequestId = m.WarrantyRequestId,
                SenderUserId = m.SenderUserId,
                SenderName = m.Sender.FullName ?? m.Sender.Username,
                Content = m.Content,
                SentAt = m.SentAt
            })
            .ToListAsync(ct);
    }

    public async Task<WarrantyChatMessageDto> SendAsync(
        int requestId, int userId, string role, string content, CancellationToken ct)
    {
        content = content.Trim();

        if (content.Length == 0 || content.Length > 2000)
            throw new ArgumentException("Tin nhắn phải có từ 1 đến 2000 ký tự.");

        if (!await CanAccessAsync(requestId, userId, role))
            throw new UnauthorizedAccessException("Bạn không có quyền gửi tin ở phiếu này.");
        if (!await IsChatOpenAsync(requestId, ct))
            throw new InvalidOperationException(
                "Phiếu đã đóng, không thể gửi tin nhắn mới.");

        var sender = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.FullName, u.Username })
            .SingleAsync(ct);

        var message = new WarrantyMessage
        {
            WarrantyRequestId = requestId,
            SenderUserId = userId,
            Content = content,
            SentAt = DateTime.UtcNow
        };

        db.WarrantyMessages.Add(message);
        await db.SaveChangesAsync(ct);

        return new WarrantyChatMessageDto
        {
            Id = message.Id,
            WarrantyRequestId = message.WarrantyRequestId,
            SenderUserId = userId,
            SenderName = sender.FullName ?? sender.Username,
            Content = message.Content,
            SentAt = message.SentAt
        };
    }
}