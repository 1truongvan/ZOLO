using WebApplication1.DTOs;
namespace WebApplication1.Services
{
    public interface IAuthService
    {
        Task<string?> LoginAsync(UserLogin dto);
    }
}
