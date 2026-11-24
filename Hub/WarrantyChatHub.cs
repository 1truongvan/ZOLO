using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WebApplication1.Services;
using WebApplication1.DTOs;
namespace WebApplication1.Hubs;

[Authorize]
public class WarrantyChatHub(IWarrantyChatService chatService) : Hub
{
    public async Task<List<WarrantyChatMessageDto>> GetHistory(int requestId)
    {
        var (userId, role) = GetCurrentUser();

        try
        {
            return await chatService.GetHistoryAsync(
                requestId, userId, role, Context.ConnectionAborted);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new HubException(ex.Message);
        }
    }
    public async Task JoinRequest(int requestId)
    {
        var (userId, role) = GetCurrentUser();

        var allowed = await chatService.CanAccessAsync(requestId, userId, role);
        if (!allowed)
            throw new HubException("Bạn không có quyền xem chat của phiếu này.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(requestId));
    }

    public async Task LeaveRequest(int requestId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(requestId));
    }

    public async Task SendMessage(int requestId, string content)
    {
        var (userId, role) = GetCurrentUser();

        try
        {
            var message = await chatService.SendAsync(
                requestId, userId, role, content, Context.ConnectionAborted);

            await Clients.Group(GroupName(requestId))
                .SendAsync("ReceiveMessage", message, Context.ConnectionAborted);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new HubException(ex.Message);
        }
        catch (ArgumentException ex)
        {
            throw new HubException(ex.Message);
        }
    }

    private (int UserId, string Role) GetCurrentUser()
    {
        var idText = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = Context.User?.FindFirstValue(ClaimTypes.Role)
                   ?? Context.User?.FindFirstValue("role");

        if (!int.TryParse(idText, out var userId) || string.IsNullOrWhiteSpace(role))
            throw new HubException("Không xác định được tài khoản đăng nhập.");

        return (userId, role);
    }

    private static string GroupName(int requestId) => $"warranty-{requestId}";
}