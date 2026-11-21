using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using WebApplication1.Data;
using WebApplication1.DTOs;
using WebApplication1.DTOs.Common;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        // 1. Khai báo biến DbContext
        private readonly AppDbContext _db;
        private readonly IUserService _userService;

        // 2. Inject AppDbContext thông qua Constructor
        public UsersController(AppDbContext db, IUserService userService)
        {
            _db = db;
            _userService = userService;
        }

        // ------------------------------------------------------------------------
        // READ ALL (GET)
        // ------------------------------------------------------------------------
        [HttpGet]
        public async Task<ActionResult<PagedResult<UserResponse>>> GetAll([FromQuery] UserQueryParameters queryParams)
        {
            var result = await _userService.GetAllAsync(queryParams);
            return Ok(result);
        }

        // ------------------------------------------------------------------------
        // READ BY ID (GET)
        // ------------------------------------------------------------------------
        [HttpGet("{id:int}")]
        public ActionResult<UserResponse> GetById(int id)
        {
            var user = _db.Users
                          .Where(u => u.Id == id)
                          .Select(u => new UserResponse
                          {
                              Id = u.Id,
                              Username = u.Username,
                              Email = u.Email,
                              Role = u.Role,
                              CreatedAt = u.CreatedAt
                          })
                          .FirstOrDefault();

            if (user == null)
            {
                return NotFound(new { message = $"Không tìm thấy người dùng có Id = {id}" });
            }

            return Ok(user);
        }

        // ------------------------------------------------------------------------
        // CREATE / REGISTER (POST)
        // ------------------------------------------------------------------------
        [HttpPost("register")]
        [AllowAnonymous]
        public ActionResult<UserResponse> Register([FromBody] UserRegister request)
        {
            bool isExist = _db.Users.Any(u => u.Username == request.Username || u.Email == request.Email);
            if (isExist)
            {
                return BadRequest(new { message = "Username hoặc Email đã được sử dụng." });
            }

            CreatePasswordHash(request.Password, out byte[] passwordHash, out byte[] passwordSalt);

            var newUser = new User
            {
                Username = request.Username,
                Email = request.Email,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                Role = "Customer",
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(newUser);
            _db.SaveChanges();

            var response = new UserResponse
            {
                Id = newUser.Id,
                Username = newUser.Username,
                Email = newUser.Email,
                Role = newUser.Role,
                CreatedAt = newUser.CreatedAt
            };

            return Ok(response);
        }

        // ------------------------------------------------------------------------
        // UPDATE (PUT)
        // ------------------------------------------------------------------------
        [HttpPut("{id:int}")]
        public IActionResult Update(int id, [FromBody] UserRegister request)
        {
            var user = _db.Users.FirstOrDefault(u => u.Id == id);

            if (user == null)
            {
                return NotFound(new { message = $"Không tìm thấy người dùng có Id = {id}" });
            }

            if (!string.IsNullOrEmpty(request.Email)) user.Email = request.Email;
            if (!string.IsNullOrEmpty(request.Username)) user.Username = request.Username;

            if (!string.IsNullOrEmpty(request.Password))
            {
                CreatePasswordHash(request.Password, out byte[] passwordHash, out byte[] passwordSalt);
                user.PasswordHash = passwordHash;
                user.PasswordSalt = passwordSalt;
            }

            _db.SaveChanges();

            return Ok(new { message = $"Cập nhật thành công người dùng Id = {id}" });
        }

        // ------------------------------------------------------------------------
        // DELETE
        // ------------------------------------------------------------------------
        [HttpDelete("{id:int}")]
        public IActionResult Delete(int id)
        {
            var user = _db.Users.FirstOrDefault(u => u.Id == id);

            if (user == null)
            {
                return NotFound(new { message = $"Không tìm thấy người dùng có Id = {id}" });
            }

            _db.Users.Remove(user);
            _db.SaveChanges();

            return Ok(new { message = $"Đã xóa người dùng Id = {id}" });
        }

        // Helper Methods
        private void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            using (var hmac = new HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            }
        }
    }
}
