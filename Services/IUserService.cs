using WebApplication1.DTOs;

using WebApplication1.DTOs.Common;

namespace WebApplication1.Services
{
    public interface IUserService
    {
        Task<UserResponse> Register(UserRegister dto);

        Task<string?> Login(UserLogin dto);

        Task<PagedResult<UserResponse>> GetAllAsync(UserQueryParameters queryParams);

        Task<UserResponse?> GetUser(int id);

        Task<bool> DeleteUser(int id);

        Task<UserProfileResponse?> GetProfileAsync(int userId);

        Task<UserProfileResponse?> UpdateOwnProfileAsync(int userId, string fullName);
    }
}
