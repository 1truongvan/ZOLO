using WebApplication1.DTOs;
using WebApplication1.Models;
using WebApplication1.Repositories;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace WebApplication1.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _repository;
        private readonly IConfiguration _config;

        public UserService(IUserRepository repository, IConfiguration config)
        {
            _repository = repository;
            _config = config;
        }

        public async Task<UserResponseDto> Register(UserRegisterDto dto)
        {
            if (await _repository.GetUsernameAsync(dto.Username) != null)
                throw new Exception("Username already exists.");

            CreatePasswordHash(dto.Password, out byte[] hash, out byte[] salt);

            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = hash,
                PasswordSalt = salt,
                Role = "Customer"
            };

            await _repository.AddAsync(user);

            return new UserResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<string?> Login(UserLoginDto dto)
        {
            var user = await _repository.GetUsernameAsync(dto.Username);

            if (user == null)
                return null;

            if (!VerifyPassword(dto.Password, user.PasswordHash, user.PasswordSalt))
                return null;

            return CreateToken(user);
        }

        public async Task<List<UserResponseDto>> GetUsers()
        {
            var users = await _repository.GetAllAsync();

            return users.Select(x => new UserResponseDto
            {
                Id = x.Id,
                Username = x.Username,
                Email = x.Email,
                Role = x.Role,
                CreatedAt = x.CreatedAt
            }).ToList();
        }

        public async Task<UserResponseDto?> GetUser(int id)
        {
            var user = await _repository.GetByIdAsync(id);

            if (user == null)
                return null;

            return new UserResponseDto
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

        private void CreatePasswordHash(string password,
            out byte[] passwordHash,
            out byte[] passwordSalt)
        {
            using var hmac = new HMACSHA512();

            passwordSalt = hmac.Key;
            passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        }

        private bool VerifyPassword(string password,
            byte[] hash,
            byte[] salt)
        {
            using var hmac = new HMACSHA512(salt);

            var computedHash = hmac.ComputeHash(
                Encoding.UTF8.GetBytes(password));

            return computedHash.SequenceEqual(hash);
        }

        private string CreateToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name,user.Username),
                new Claim(ClaimTypes.Role,user.Role)
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

            var creds = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha512Signature);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.Now.AddHours(12),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}