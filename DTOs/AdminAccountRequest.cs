using System.ComponentModel.DataAnnotations;

namespace WebApplication1.DTOs;

public class AdminAccountRequest
{
    [Required, StringLength(50, MinimumLength = 3)] public string Username { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = string.Empty;
    [StringLength(120)] public string? FullName { get; set; }
    [Phone, StringLength(30)] public string? PhoneNumber { get; set; }
    [StringLength(200, MinimumLength = 12)] public string? Password { get; set; }
    [Required] public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;
}
