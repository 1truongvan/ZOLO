
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;
using WebApplication1.Data;
using WebApplication1.Repositories;
using WebApplication1.Seeding;
using WebApplication1.Services;

var builder = WebApplication.CreateBuilder(args);

// ========================================
// 1. KẾT NỐI DATABASE
// ========================================

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// ========================================
// 2. ĐĂNG KÝ CONTROLLERS
// ========================================

builder.Services.AddControllers();

// ========================================
// 3. DEPENDENCY INJECTION
// ========================================

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IModulePermissionService, ModulePermissionService>();

// ========================================
// 4. ĐỌC CẤU HÌNH JWT
// ========================================

var jwtSettings = builder.Configuration.GetSection("JwtSettings");

var secretKey = jwtSettings["SecretKey"];
var jwtIssuer = jwtSettings["Issuer"];
var jwtAudience = jwtSettings["Audience"];

if (string.IsNullOrWhiteSpace(secretKey))
{
    throw new InvalidOperationException(
        "Thiếu JwtSettings:SecretKey. Hãy cấu hình JwtSettings__SecretKey bằng biến môi trường.");
}

if (string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException(
        "JwtSettings:Issuer và JwtSettings:Audience không được để trống.");
}

if (Encoding.UTF8.GetByteCount(secretKey) < 32)
{
    throw new InvalidOperationException(
        "JwtSettings:SecretKey phải có ít nhất 32 byte.");
}

// ========================================
// 5. CẤU HÌNH JWT AUTHENTICATION
// ========================================

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
        JwtBearerDefaults.AuthenticationScheme;

    options.DefaultChallengeScheme =
        JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters =
        new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secretKey)),

            // Phân quyền Admin / User
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var idValue = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idValue, out var userId))
            {
                context.Fail("Invalid account claim.");
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var account = await db.Users.AsNoTracking().Where(user => user.Id == userId)
                .Select(user => new { user.Role, user.IsActive }).FirstOrDefaultAsync(context.HttpContext.RequestAborted);
            var tokenRole = context.Principal?.FindFirst(ClaimTypes.Role)?.Value;
            if (account is null || !account.IsActive || !string.Equals(account.Role, tokenRole, StringComparison.OrdinalIgnoreCase))
                context.Fail("The account is disabled or its role has changed.");
        }
    };
});

// ========================================
// 6. AUTHORIZATION
// ========================================

builder.Services.AddAuthorization();

// ========================================
// 7. SWAGGER + AUTHORIZE BUTTON
// ========================================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WebApplication1",
        Version = "v1"
    });

    // Hiển thị nút Authorize
    options.AddSecurityDefinition("Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Nhập JWT Token sau khi Login."
        });

    // Áp dụng Bearer Token cho Swagger
    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
});

// ========================================
// 8. BUILD APPLICATION
// ========================================

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await AdminCatalogSeeder.SeedAsync(db);
        await SystemPermissionCatalog.SeedAsync(db);
    }
    await DevelopmentAdminSeeder.SeedAsync(
        app.Services,
        app.Configuration,
        app.Logger);
}

// ========================================
// 9. SWAGGER UI
// ========================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "WebApplication1 v1"
        );

        options.RoutePrefix = "swagger";
    });
}

// ========================================
// 10. MIDDLEWARE
// ========================================

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

// Authentication phải đứng trước Authorization
app.UseAuthentication();

app.UseAuthorization();

// ========================================
// 11. CONTROLLERS
// ========================================

app.MapControllers();
app.MapGet("/login", () => Results.Redirect("/login.html"));
app.MapGet("/register", () => Results.Redirect("/register.html"));
app.MapGet("/admin/dashboard", () => Results.Redirect("/admin/dashboard.html"));
foreach (var adminModule in new[]
{
    "accounts", "account-roles", "brands", "categories", "products", "warehouses", "receipts", "shipments", "inventory",
    "warranty-types", "fault-groups", "service-prices", "travel-prices", "warranty-requests", "components", "stations",
    "station-parts", "component-receipts", "allocations", "component-proposals", "dealers"
})
{
    app.MapGet($"/admin/{adminModule}", () => Results.Redirect($"/admin/dashboard.html?module={adminModule}"));
}
app.MapGet("/user", () => Results.Redirect("/user/dashboard"));
app.MapGet("/staff/dashboard", () => Results.Redirect("/staff/dashboard.html"));
app.MapGet("/manager/dashboard", () => Results.Redirect("/manager/dashboard.html"));
app.MapGet("/technician/dashboard", () => Results.Redirect("/technician/dashboard.html"));

var userDashboardFile = Path.Combine(app.Environment.WebRootPath, "user", "index.html");
foreach (var userPage in new[]
{
    "dashboard", "products", "warranty-register", "warranty-request",
    "requests", "history", "profile"
})
{
    app.MapGet($"/user/{userPage}", async (HttpContext context) =>
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.SendFileAsync(userDashboardFile);
    });
}

// ========================================
// 12. RUN
// ========================================

app.Run();
