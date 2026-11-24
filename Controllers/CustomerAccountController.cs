using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.DTOs;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

[ApiController]
[Route("api/account")]
[Authorize(Roles = "Customer,User")]
public sealed class CustomerAccountController(IUserService userService) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var profile = await userService.GetProfileAsync(userId);
        return profile is null ? Unauthorized() : Ok(profile);
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe(UpdateOwnProfileRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        try
        {
            var profile = await userService.UpdateOwnProfileAsync(userId, request.FullName);
            return profile is null ? Unauthorized() : Ok(profile);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
