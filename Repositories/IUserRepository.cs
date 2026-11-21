using WebApplication1.Models;

using WebApplication1.DTOs.Common;

namespace WebApplication1.Repositories
{
    public interface IUserRepository
    {
        Task<PagedResult<User>> GetAllAsync(UserQueryParameters queryParams);

        Task<User?> GetByIdAsync(int id);

        Task<User?> GetUsernameAsync(string username);

        Task<User?> GetByEmailAsync(string email);

        Task AddAsync(User user);

        Task UpdateAsync(User user);

        Task DeleteAsync(User user);
    }
}
