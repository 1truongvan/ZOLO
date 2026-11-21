using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Seeding
{
    public static class DevelopmentAdminSeeder
    {
        public static async Task SeedAsync(
            IServiceProvider services,
            IConfiguration configuration,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (!await db.Database.CanConnectAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "Không thể kết nối database cấu hình cho Development. Không tạo tài khoản Admin.");
            }

            var existingAdminName = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user => user.Username == "admin", cancellationToken);

            if (existingAdminName is not null)
            {
                if (string.Equals(existingAdminName.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    if (configuration.GetValue<bool>("EWarrantyAdmin:ResetPassword"))
                    {
                        var replacementPassword = configuration["EWarrantyAdmin:Password"];
                        if (string.IsNullOrWhiteSpace(replacementPassword) || replacementPassword.Length < 16)
                        {
                            throw new InvalidOperationException(
                                "Đặt EWarrantyAdmin__Password (ít nhất 16 ký tự) cùng EWarrantyAdmin__ResetPassword=true để đổi mật khẩu Admin.");
                        }

                        CreatePasswordHash(replacementPassword, out var replacementHash, out var replacementSalt);
                        existingAdminName.PasswordHash = replacementHash;
                        existingAdminName.PasswordSalt = replacementSalt;
                        db.Users.Update(existingAdminName);
                        await db.SaveChangesAsync(cancellationToken);
                        logger.LogInformation(
                            "Đã đổi password hash cho Development Admin theo yêu cầu tường minh; password không được ghi log.");
                        return;
                    }

                    logger.LogInformation("Tài khoản Development admin đã tồn tại; bỏ qua khởi tạo.");
                }
                else
                {
                    logger.LogError(
                        "Username admin đã thuộc tài khoản role {Role}; giữ nguyên tài khoản và không tạo trùng.",
                        existingAdminName.Role);
                }

                return;
            }

            var password = configuration["EWarrantyAdmin:Password"];
            if (string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning(
                    "Chưa khởi tạo tài khoản Development Admin. Hãy đặt biến môi trường EWarrantyAdmin__Password (ít nhất 16 ký tự) rồi khởi động lại.");
                return;
            }

            if (password.Length < 16)
            {
                throw new InvalidOperationException(
                    "EWarrantyAdmin__Password phải có ít nhất 16 ký tự.");
            }

            var email = configuration["EWarrantyAdmin:Email"] ?? "admin@ewarranty.local";
            var emailInUse = await db.Users
                .AsNoTracking()
                .AnyAsync(user => user.Email == email, cancellationToken);

            if (emailInUse)
            {
                throw new InvalidOperationException(
                    "Email mặc định Admin đã được tài khoản khác sử dụng. Hãy đặt EWarrantyAdmin__Email khác.");
            }

            CreatePasswordHash(password, out var passwordHash, out var passwordSalt);
            var admin = new User
            {
                Username = "admin",
                Email = email,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                Role = "Admin",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            db.Users.Add(admin);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Đã khởi tạo tài khoản Development Admin. Username={Username}; password không được ghi log.",
                    admin.Username);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                var adminCreatedByAnotherInstance = await db.Users
                    .AsNoTracking()
                    .AnyAsync(user => user.Username == "admin" && user.Role == "Admin", cancellationToken);

                if (adminCreatedByAnotherInstance)
                {
                    logger.LogInformation("Tài khoản Development Admin đã được tạo đồng thời; bỏ qua.");
                    return;
                }

                throw;
            }
        }

        private static void CreatePasswordHash(
            string password,
            out byte[] passwordHash,
            out byte[] passwordSalt)
        {
            using var hmac = new HMACSHA512();
            passwordSalt = hmac.Key;
            passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        }
    }
}
