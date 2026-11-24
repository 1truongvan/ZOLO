using System.ComponentModel.DataAnnotations;

namespace WebApplication1.DTOs;

public sealed class UpdateOwnProfileRequest
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(120, MinimumLength = 1, ErrorMessage = "Họ và tên tối đa 120 ký tự.")]
    public string FullName { get; set; } = string.Empty;
}
