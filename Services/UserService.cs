using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using WebApplication1.DTOs;
using WebApplication1.DTOs.Common;
using WebApplication1.Models;
using WebApplication1.Repositories;

namespace WebApplication1.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _repository;
        private readonly IConfiguration _config;

        public UserService(
            IUserRepository repository,
            IConfiguration config)
        {
            _repository = repository;
            _config = config;
        }

        public async Task<UserResponse> Register(UserRegister dto)
        {
            if (await _repository.GetUsernameAsync(dto.Username) != null)
                throw new Exception("Username already exists.");

            CreatePasswordHash(
                dto.Password,
                out byte[] hash,
                out byte[] salt);

            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = hash,
                PasswordSalt = salt,
                Role = "Customer"
            };

            await _repository.AddAsync(user);

            return new UserResponse
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<string?> Login(UserLogin dto)
        {
            var user = await _repository.GetUsernameAsync(dto.Username);

            if (user == null || !user.IsActive)
                return null;

            if (!VerifyPassword(
                dto.Password,
                user.PasswordHash,
                user.PasswordSalt))
            {
                return null;
            }

            return CreateToken(user);
        }

        public async Task<PagedResult<UserResponse>> GetAllAsync(UserQueryParameters queryParams)
        {
            var pagedUsers = await _repository.GetAllAsync(queryParams);
            var mappedItems = pagedUsers.Items.Select(MapToResponseDto).ToList();

            return new PagedResult<UserResponse>(
                mappedItems,
                pagedUsers.TotalCount,
                pagedUsers.PageNumber,
                pagedUsers.PageSize);
        }

        private static UserResponse MapToResponseDto(User user)
        {
            return new UserResponse
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<UserResponse?> GetUser(int id)
        {
            var user = await _repository.GetByIdAsync(id);

            if (user == null)
                return null;

            return new UserResponse
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<bool> DeleteUser(int id)
        {
            var user = await _repository.GetByIdAsync(id);

            if (user == null)
                return false;

            await _repository.DeleteAsync(user);

            return true;
        }

        public async Task<UserProfileResponse?> GetProfileAsync(int userId)
        {
            var user = await _repository.GetByIdAsync(userId);
            if (user is null || !user.IsActive) return null;

            return new UserProfileResponse
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<UserProfileResponse?> UpdateOwnProfileAsync(int userId, string fullName)
        {
            var normalizedName = fullName?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
                throw new ArgumentException("Họ và tên không được để trống.");
            if (normalizedName.Length > 120)
                throw new ArgumentException("Họ và tên tối đa 120 ký tự.");

            var user = await _repository.GetByIdAsync(userId);
            if (user is null || !user.IsActive) return null;

            user.FullName = normalizedName;
            await _repository.UpdateAsync(user);

            return new UserProfileResponse
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };
        }

        private void CreatePasswordHash(
            string password,
            out byte[] passwordHash,
            out byte[] passwordSalt)
        {
            using var hmac = new HMACSHA512();

            passwordSalt = hmac.Key;

            passwordHash = hmac.ComputeHash(
                Encoding.UTF8.GetBytes(password));
        }

        private bool VerifyPassword(
            string password,
            byte[] hash,
            byte[] salt)
        {
            using var hmac = new HMACSHA512(salt);

            var computedHash = hmac.ComputeHash(
                Encoding.UTF8.GetBytes(password));

            return CryptographicOperations.FixedTimeEquals(computedHash, hash);
        }

        private string CreateToken(User user)
        {
            var jwtSettings = _config.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"]
                ?? throw new InvalidOperationException("Missing JwtSettings:SecretKey configuration.");
            var issuer = jwtSettings["Issuer"]
                ?? throw new InvalidOperationException("Missing JwtSettings:Issuer configuration.");
            var audience = jwtSettings["Audience"]
                ?? throw new InvalidOperationException("Missing JwtSettings:Audience configuration.");
            if (!double.TryParse(
                    jwtSettings["ExpiryMinutes"],
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var expiryMinutes)
                || expiryMinutes <= 0)
            {
                throw new InvalidOperationException(
                    "JwtSettings:ExpiryMinutes phải là số dương hợp lệ.");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secretKey));

            var creds = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }
    }
}
