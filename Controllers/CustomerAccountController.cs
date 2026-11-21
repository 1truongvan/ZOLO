using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/account")]
[Authorize(Roles = "Customer,User")]
public sealed class CustomerAccountController(AppDbContext db) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var account = await db.Users.AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive)
            .Select(user => new { user.Id, user.Username, user.FullName, user.Email, user.PhoneNumber, user.Role, user.CreatedAt })
            .FirstOrDefaultAsync(ct);
        return account is null ? Unauthorized() : Ok(account);
    }
}
